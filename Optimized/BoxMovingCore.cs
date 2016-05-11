using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KakuBoxMoving.Optimized
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
            var r = RunToStopParallel(stopIndicator);
            Console.WriteLine(this.queue.HashIndexCount);
            return r;
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
    public class PointToPointPathFinder
    {
        IMap map;
        Point start;
        public PointToPointPathFinder(IMap map, Point start)
        {
            this.map = map;
            this.start = start;
        }
        public bool HasPath(Point end)
        {
            List<Point> states = new List<Point>();
            states.Add(start);
            int currentIdx = 0;
            while (currentIdx < states.Count)
            {
                var current = states[currentIdx];

                var left = new Point(current.X - 1, current.Y);
                if (left == end) return true;
                if (map.IsValidPoint(left) && !states.Exists(s => s == left)) states.Add(left);

                var right = new Point(current.X + 1, current.Y);
                if (right == end) return true;
                if (map.IsValidPoint(right) && !states.Exists(s => s == right)) states.Add(right);

                var up = new Point(current.X, current.Y + 1);
                if (up == end) return true;
                if (map.IsValidPoint(up) && !states.Exists(s => s == up)) states.Add(up);

                var down = new Point(current.X, current.Y - 1);
                if (down == end) return true;
                if (map.IsValidPoint(down) && !states.Exists(s => s == down)) states.Add(down);

                currentIdx++;
            }
            return false;
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
            return (x << 8) | y;
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

    //public sealed class Point
    //{
    //    private int x;
    //    private int y;

    //    static Dictionary<int, Point> pool = new Dictionary<int, Point>();
    //    public int X { get { return x; } }
    //    public int Y { get { return y; } }

    //    public static Point Make(int x, int y)
    //    {
    //        int code = (x << 8) | y;
    //        Point p;
    //        if(!pool.TryGetValue(code, out p))
    //        {
    //            p = new Point(x, y);
    //            pool.Add(code, p);
    //        }
    //        return p;
    //    }
    //    private Point(int x, int y)
    //    {
    //        this.x = x;
    //        this.y = y;
    //    }

    //    public override string ToString()
    //    {
    //        return string.Format("({0},{1})", x.ToString(), y.ToString());
    //    }
    //}

    public class BoxState
    {
        private Point[] boxs;

        public Point[] Boxs { get { return this.boxs; } }
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
        public BoxState(Point[] boxs)
        {
            this.boxs = boxs;
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
            Point[] r = new Point[this.boxs.Length];
            this.boxs.CopyTo(r, 0);
            return r;
        }

        public bool HasBox(Point point)
        {
            return boxs.Any(b => b == point);
        }

        public virtual bool IsMeetMap(IMap map)
        {
            return boxs.All(p => map.IsValidPoint(p));
        }
        public override int GetHashCode()
        {
            return GetBoxStateHashCode();
        }
        public override bool Equals(Object obj)
        {
            BoxState s = obj as BoxState;

            if (object.ReferenceEquals(s, null)) return false;

            return this.IsBoxStateEquals(s);
        }

        public bool IsBoxStateEquals(BoxState state)
        {
            if (state == null || state.boxs.Length != this.boxs.Length) return false;

            var s = boxs.ToList();
            var d = state.boxs.ToList();
            for(int i=0;i<boxs.Length;i++)
            {
                if (!d.Remove(s[i])) return false;
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

        public int GetBoxStateHashCode()
        {
            //return boxs.Aggregate(0, (seed, p) => seed ^= p.GetHashCode());
            //return boxs.Aggregate(0, (seed, p) => seed += ((p.X<<4) | p.Y));

            return boxs.Aggregate(0, (seed, p) => seed ^= hash32shift((p.X << 8) | p.Y));

            //int code = 0;
            //int boxbits = 16 / boxs.Length;
            //int codebits = boxbits * 2;
            //for (int i = 0; i < boxs.Length; i++)
            //{
            //    code = (code << codebits) | (boxs[i].X << boxbits) | boxs[i].Y;
            //}
            //return code;
        }
        //int hash(int a)
        //{
        //    a = (a + 0x7ed55d16) + (a << 12);
        //    a = (a ^ 0xc761c23c) ^ (a >> 19);
        //    a = (a + 0x165667b1) + (a << 5);
        //    a = (a + 0xd3a2646c) ^ (a << 9);
        //    a = (a + 0xfd7046c5) + (a << 3); // <<和 +的组合是可逆的 
        //    a = (a ^ 0xb55a4f09) ^ (a >> 16);
        //    return a;
        //}
        int hash32shift(int key)
        {
            key = ~key + (key << 15); // key = (key << 15) - key - 1; 
            key = key ^ (key >> 12);
            key = key + (key << 2);
            key = key ^ (key >> 4);
            key = (key + (key << 3)) + (key << 11); //key = key * 2057; //
            key = key ^ (key >> 16);
            return key;
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

        //Hash index for searching acceleration
        private Dictionary<int, List<T>> indexedHash = new Dictionary<int, List<T>>(1024);

        public int HashIndexCount { get { return indexedHash.Count; } }


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

            var hashcode = state.GetBoxStateHashCode();

            List<T> temp;

            if(this.indexedHash.TryGetValue(hashcode, out temp))
            {
                temp.Add(state);
            }
            else
            {
                temp = new List<T>();
                temp.Add(state);
                indexedHash.Add(hashcode, temp);
            }
        }
        public void AddRange(IEnumerable<T> range)
        {
            if (range == null) throw new ArgumentNullException("range");

            foreach (var state in range)
            {
                if (state != null) Add(state);
            }
        }

        public List<T> FindSameBoxState(BoxState state)
        {
            if (state == null) return new List<T>();

            var hashcode = state.GetBoxStateHashCode();

            List<T> indexedList;

            if (this.indexedHash.TryGetValue(hashcode, out indexedList))
            {
                return indexedList.FindAll(s => s.IsBoxStateEquals(state));
            }
            
            return new List<T>();
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

    public interface IMap
    {
        bool IsValidPoint(Point point);
    }
    public class Map : IMap
    {
        public int XUpBound { get; set; }
        public int XDownBound { get; set; }
        public int YUpBound { get; set; }
        public int YDownBound { get; set; }
        public HashSet<Point> Barriers { get; private set; }

        public Map(IEnumerable<Point> barriers)
        {
            Barriers = new HashSet<Point>(barriers);
        }
        public bool IsValidPoint(Point point)
        {
            return point.X >= XDownBound && point.X <= XUpBound && point.Y >= YDownBound && point.Y <= YUpBound && !Barriers.Contains(point);
        }
    }
    public class ExtendedMap : IMap
    {
        private IMap baseMap;
        public Point[] ExtendedBarriers { get; private set; }

        public ExtendedMap(IMap map, Point[] extendedBarriers)
        {
            this.baseMap = map;
            this.ExtendedBarriers = extendedBarriers;
        }

        public bool IsValidPoint(Point point)
        {
            if (!this.baseMap.IsValidPoint(point)) return false;

            for (int i = 0; i < ExtendedBarriers.Length; i++)
            {
                if (ExtendedBarriers[i] == point) return false;
            }
            return true;
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
        private IMap map;
        public MapCheckPolicy(IMap map)
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
