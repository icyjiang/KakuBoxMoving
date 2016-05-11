using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KakuBoxMoving
{
    public abstract class MovingPathFinder<T> where T : BoxState
    {
        protected Map map;
        protected T start;
        protected StateQueue<T> queue;
        protected MovingChecker checker;

        public int QueueCount { get { return this.queue.Count(); } }

        public MovingPathFinder(Map map, T start)
        {
            if (map == null) throw new ArgumentNullException("map");
            if (start.IsOverlapping) throw new ArgumentException("start is overlapping.");
            if (!start.IsMeetMap(map)) throw new ArgumentException("start does not meet the map.");

            this.map = map;
            this.start = start;

            queue = new StateQueue<T>();

            queue.Add(start);

            checker = new MovingChecker();
        }

        private bool ValidEnd(BoxState end)
        {
            if (end == null) throw new ArgumentNullException("end");
            if (end.IsOverlapping) return false;
            if (start.BoxCount != end.BoxCount) return false;
            if (!end.IsMeetMap(map)) return false;
            return true;
        }

        public bool HasPath(BoxState end)
        {
            if (!ValidEnd(end)) return false;

            if (this.queue.FindSameBoxState(end).Count > 0) return true;

            return RunToStop(state => state.IsBoxStateEquals(end));
        }

        public void Run()
        {
            RunToStop(null);
        }

        public List<BoxState> FindPath(BoxState end)
        {
            List<BoxState> path = new List<BoxState>();

            if(this.HasPath(end))
            {
                BoxState find = this.queue.FindSameBoxState(end).First();

                path.Add(find);

                while (find.Parent != null)
                {
                    path.Add(find.Parent);
                    find = find.Parent;
                }

                path.Reverse();
            }
            return path;
        }

        private bool RunToStop(Func<T, bool> stopIndicator)
        {
            if (this is KakuBoxMovingFinder)
            {
                return RunToStopParallel(stopIndicator);
            }          

            return RunToStopSingle(stopIndicator);
        }
        private bool RunToStopSingle(Func<T, bool> stopIndicator)
        {
            bool stop = false;
            while (queue.HasNext && !stop)
            {
                var current = queue.Next();
                var steps = current.GetAllNextMoving();
                foreach (var step in steps)
                {
                    if (checker.Check(step))
                    {
                        queue.Add((T)step.Final);

                        current.Children.Add(step.Final);

                        step.Final.Parent = current;

                        if (!stop && stopIndicator != null) stop = stopIndicator((T)step.Final);
                    }
                }
            }
            return stop;
        }
        
        private bool RunToStopParallel(Func<T, bool> stopIndicator)
        {
            bool stop = false;
            while (queue.HasNext && !stop)
            {
                var current = queue.Next();
                var steps = current.GetAllNextMoving();

                var temList = new List<T>();

                Parallel.ForEach(steps, () => new List<T>(), (step, loopState, localList) =>
                {
                    if (checker.Check(step))
                    {
                        localList.Add((T)step.Final);

                        step.Final.Parent = current;

                        if (!stop && stopIndicator != null && stopIndicator((T)step.Final)) stop = true;
                    }
                    return localList;
                },
                localList =>
                {
                    lock (temList)
                    {
                        temList.AddRange(localList);
                    }
                });
                current.Children.AddRange(temList);
                this.queue.AddRange(temList);
            }
            return stop;
        }
    }
    public class PointToPointPathFinder : MovingPathFinder<BoxState>
    {
        public PointToPointPathFinder(Map map, Point start)
            : base(map, new BoxState(new Point[] { start }))
        {
            var p1 = new MapCheckPolicy(this.map);
            var p2 = new DuplicationsDetectPolicy(this.queue);

            this.checker.AddPolicy(p1);
            this.checker.AddPolicy(p2);
        }
        public bool HasPath(Point end)
        {
            BoxState endState = new BoxState(new Point[] { end });
            return this.HasPath(endState);
        }
    }

    public struct Point
    {
        private int x;
        private int y;
        public int X { get { return x; } }
        public int Y { get { return y; } }
        public Point(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
        public override int GetHashCode()
        {
            return x ^ y;
        }
        public override bool Equals(Object obj)
        {
            return obj is Point && this == (Point)obj;
        }

        public static bool operator ==(Point first, Point second)
        {
            return first.x == second.x && first.y == second.y;
        }
        public static bool operator !=(Point first, Point second)
        {
            return !(first == second);
        }

        public override string ToString()
        {
            return string.Format("({0},{1})", x.ToString(), y.ToString());
        }
    }

    public class BoxState
    {
        private Point[] boxs;

        public BoxState Parent { get; set; }

        public List<BoxState> Children { get; private set; }

        public int BoxCount { get { return boxs.Length; } }

        public virtual bool IsOverlapping
        {
            get { return boxs.Length != boxs.Distinct().Count(); }
        }

        public Point this[int idx]
        {
            get
            {
                return boxs[idx];
            }
        }
        private string idd;
        public BoxState(IEnumerable<Point> boxs)
        {
            if (boxs.Count() == 0) throw new ArgumentException("boxs is empty.", "boxs");

            this.boxs = boxs.OrderBy(point => point, Comparer<Point>.Create((p1, p2) =>
                    {
                        if (p1.X > p2.X) return 1;
                        if (p1.X < p2.X) return -1;
                        if (p1.Y > p2.Y) return 1;
                        if (p1.Y < p2.Y) return -1;
                        return 0;
                    })).ToArray();
            this.Children = new List<BoxState>();
        }

        public List<BoxMovingStep> GetAllNextMoving()
        {
            List<BoxMovingStep> result = new List<BoxMovingStep>();

            for (int i = 0; i < BoxCount; i++)
            {
                result.Add(MakeStep(i, BoxMovingDirection.Up));
                result.Add(MakeStep(i, BoxMovingDirection.Down));
                result.Add(MakeStep(i, BoxMovingDirection.Left));
                result.Add(MakeStep(i, BoxMovingDirection.Right));
            }
            return result;
        }

        protected virtual BoxMovingStep MakeStep(int boxIdx, BoxMovingDirection direction)
        {
            return new BoxMovingStep(this, boxIdx, direction);
        }

        public Point[] Copy()
        {
            return boxs.ToArray();
        }

        public bool HasBox(Point point)
        {
            return boxs.Any(b => b == point);
        }

        public virtual bool IsMeetMap(Map map)
        {
            return boxs.All(p => map.IsValidPoint(p));
        }
        public override int GetHashCode()
        {
            return boxs.Aggregate(0, (seed, p) => seed ^= p.GetHashCode());
        }
        public override bool Equals(Object obj)
        {
            BoxState s = obj as BoxState;

            if (object.ReferenceEquals(s, null)) return false;

            return this.IsBoxStateEquals(s);
        }

        public bool IsBoxStateEquals(BoxState state)
        {
            if (state == null) return false;
            if (state.boxs.Length != this.boxs.Length) return false;

            for(int i=0;i<boxs.Length;i++)
            {
                if (boxs[i] != state.boxs[i]) return false;
            }
            return true;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            foreach (var box in boxs)
            {
                sb.Append("(");
                sb.Append(box.X.ToString());
                sb.Append(",");
                sb.Append(box.Y.ToString());
                sb.Append(")");
            }
            return sb.ToString();
        }
    }

    public class BoxMovingStep
    {
        public BoxState Orignal { get; private set; }

        public int MovingBoxIndex { get; private set; }

        public BoxMovingDirection MovingDirection { get; private set; }

        private BoxState final;
        public BoxState Final
        {
            get
            {
                if (final == null) final = Move();
                return final;
            }
        }

        public BoxMovingStep(BoxState state, int movingBoxIndex, BoxMovingDirection boxMovingDirection)
        {
            if (state == null) throw new ArgumentNullException("state");

            if (movingBoxIndex < 0 || movingBoxIndex >= state.BoxCount) throw new ArgumentOutOfRangeException("movingBoxIndex");

            Orignal = state;
            MovingBoxIndex = movingBoxIndex;
            MovingDirection = boxMovingDirection;
        }

        private BoxState Move()
        {
            var copy = Orignal.Copy();

            var movingBox = copy[MovingBoxIndex];

            switch (MovingDirection)
            {
                case BoxMovingDirection.Up:
                    copy[MovingBoxIndex] = new Point(movingBox.X, movingBox.Y + 1);
                    break;
                case BoxMovingDirection.Down:
                    copy[MovingBoxIndex] = new Point(movingBox.X, movingBox.Y - 1);
                    break;
                case BoxMovingDirection.Left:
                    copy[MovingBoxIndex] = new Point(movingBox.X - 1, movingBox.Y);
                    break;
                case BoxMovingDirection.Right:
                    copy[MovingBoxIndex] = new Point(movingBox.X + 1, movingBox.Y);
                    break;
                default: break;
            }

            return MakeState(copy);
        }
        protected virtual BoxState MakeState(Point[] boxs)
        {
            return new BoxState(boxs);
        }
    }
    public class StateQueue<T> : IEnumerable<T> where T : BoxState
    {
        private List<T> list;
        private int current = -1;

        public StateQueue()
        {
            list = new List<T>();
        }
        public bool HasNext
        {
            get
            {
                return current + 1 < list.Count;
            }
        }
        public T Next()
        {
            if (HasNext) return list[++current];
            return null;
        }
        public void Add(T state)
        {
            if (state == null) throw new ArgumentNullException("state");

            list.Add(state);
        }
        public void AddRange(IEnumerable<T> range)
        {
            if (range == null) throw new ArgumentNullException("range");

            foreach (var state in range)
            {
                if (state != null) list.Add(state);
            }
        }

        public List<T> FindSameBoxState(BoxState state)
        {
            if (state == null) return new List<T>();

            return list.FindAll(s => s.IsBoxStateEquals(state));
        }

        #region IEnumerable<BoxState> Members

        public IEnumerator<T> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }

        #endregion
    }
    public class Map
    {
        public int XUpBound { get; set; }
        public int XDownBound { get; set; }
        public int YUpBound { get; set; }
        public int YDownBound { get; set; }
        public List<Point> Barriers { get; set; }

        public Map()
        {
            Barriers = new List<Point>();
        }
        public bool IsValidPoint(Point point)
        {
            return point.X >= XDownBound && point.X <= XUpBound && point.Y >= YDownBound && point.Y <= YUpBound && !Barriers.Any(b => b == point);
        }

        public Map Clone()
        {
            Map newMap = new Map();
            newMap.XUpBound = XUpBound;
            newMap.XDownBound = XDownBound;
            newMap.YUpBound = YUpBound;
            newMap.YDownBound = YDownBound;
            newMap.Barriers.AddRange(Barriers);
            return newMap;
        }
    }

    public class MovingChecker : IMovingCheckingPolicy
    {
        private List<IMovingCheckingPolicy> policies = new List<IMovingCheckingPolicy>();

        #region MovingCheckingPolicy Members

        public void AddPolicy(IMovingCheckingPolicy policy)
        {
            if (policy == null) throw new ArgumentNullException("policy");

            policies.Add(policy);
        }

        public bool Check(BoxMovingStep movingStep)
        {
            return policies.All(p => p.Check(movingStep));
        }

        #endregion
    }

    public class MapCheckPolicy : IMovingCheckingPolicy
    {
        private Map map;
        public MapCheckPolicy(Map map)
        {
            if (map == null) throw new ArgumentNullException("map");
            this.map = map;
        }
        public bool Check(BoxMovingStep movingStep)
        {
            return movingStep.Final.IsMeetMap(map);
        }
    }

    public class OverlappingCheckPolicy : IMovingCheckingPolicy
    {
        public bool Check(BoxMovingStep movingStep)
        {
            return !movingStep.Final.IsOverlapping;
        }
    }

    public class DuplicationsDetectPolicy : IMovingCheckingPolicy
    {
        private StateQueue<BoxState> queue;

        public DuplicationsDetectPolicy(StateQueue<BoxState> queue)
        {
            if (queue == null) throw new ArgumentNullException("queue");

            this.queue = queue;
        }
        public bool Check(BoxMovingStep movingStep)
        {
            return this.queue.FindSameBoxState(movingStep.Final).Count == 0;
        }
    }
    
    public interface IMovingCheckingPolicy
    {
        bool Check(BoxMovingStep movingStep);
    }
    public enum BoxMovingDirection
    {
        Up,
        Down,
        Left,
        Right
    }
}
