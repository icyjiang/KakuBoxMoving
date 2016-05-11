using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KakuBoxMoving.Optimized
{
    public class KakuBoxState : BoxState
    {
        private Point kaku;
        public Point Kaku { get { return this.kaku; } }

        public KakuBoxState(Point kaku, Point[] boxs)
            : base(boxs)
        {
            this.kaku = kaku;
        }

        public override bool IsOverlapping
        {
            get
            {
                return base.IsOverlapping || this.HasBox(kaku);
            }
        }
        public override bool IsMeetMap(IMap map)
        {
            return map.IsValidPoint(kaku) && base.IsMeetMap(map);
        }

        protected override BoxMovingStep MakeStep(int boxIdx, BoxMovingDirection direction)
        {
            return new KakuBoxMovingStep(this, boxIdx, direction);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode() ^ kaku.GetHashCode();
        }
        public override bool Equals(Object obj)
        {
            KakuBoxState s = obj as KakuBoxState;

            if (object.ReferenceEquals(s, null)) return false;

            return kaku == s.kaku && base.Equals(obj);
        }
        public override string ToString()
        {
            return string.Format("{{({0},{1})-{2}}}", kaku.X, kaku.Y, base.ToString());
        }
    }

    public class KakuBoxMovingStep : BoxMovingStep
    {
        public KakuBoxMovingStep(KakuBoxState state, int movingBoxIndex, BoxMovingDirection boxMovingDirection)
            : base(state, movingBoxIndex, boxMovingDirection)
        {
            var movingBox = state[movingBoxIndex];

            switch (boxMovingDirection)
            {
                case BoxMovingDirection.Up:
                    kakuPushingPoint = new Point(movingBox.X, movingBox.Y - 1);
                    break;
                case BoxMovingDirection.Down:
                    kakuPushingPoint = new Point(movingBox.X, movingBox.Y + 1);
                    break;
                case BoxMovingDirection.Left:
                    kakuPushingPoint = new Point(movingBox.X + 1, movingBox.Y);
                    break;
                case BoxMovingDirection.Right:
                    kakuPushingPoint = new Point(movingBox.X - 1, movingBox.Y);
                    break;
                default: break;
            }
        }

        public Point kakuPushingPoint;
        public Point KakuPushingPoint
        {
            get
            {
                return kakuPushingPoint;
            }
        }
        protected override BoxState MakeState(Point[] boxs)
        {
            return new KakuBoxState(Orignal[MovingBoxIndex], boxs);
        }
    }

    public class KakuMovingCheckPolicy : IMovingCheckingPolicy
    {
        private IMap map;

        public KakuMovingCheckPolicy(IMap map)
        {
            if (map == null) throw new ArgumentNullException("map");
            this.map = map;
        }
        public bool Check(BoxMovingStep movingStep)
        {
            KakuBoxMovingStep thisStep = movingStep as KakuBoxMovingStep;

            if (thisStep == null) throw new InvalidOperationException("KakuMovingCheckPolicy only can check non null KakuBoxMovingStep.");

            if (((KakuBoxState)thisStep.Orignal).Kaku == thisStep.KakuPushingPoint) return true;

            var newMap = new ExtendedMap(this.map, thisStep.Orignal.Boxs);

            if (!newMap.IsValidPoint(thisStep.kakuPushingPoint)) return false;

            PointToPointPathFinder pathFinder = new PointToPointPathFinder(newMap, ((KakuBoxState)thisStep.Orignal).Kaku);

            return pathFinder.HasPath(thisStep.KakuPushingPoint);
        }
    }

    public class KakuMovingDuplicationsDetectPolicy : IMovingCheckingPolicy
    {
        private IMap map;
        private StateQueue<KakuBoxState> queue;

        public KakuMovingDuplicationsDetectPolicy(IMap map, StateQueue<KakuBoxState> queue)
        {
            if (map == null) throw new ArgumentNullException("map");
            if (queue == null) throw new ArgumentNullException("queue");

            this.map = map;
            this.queue = queue;
        }
        public bool Check(BoxMovingStep movingStep)
        {
            KakuBoxState state = (KakuBoxState)movingStep.Final;

            var finds = queue.FindSameBoxState(state);

            if (finds.Count > 0)
            {
                foreach (var find in finds)
                {
                    if (find.Kaku.Equals(state.Kaku))
                    {
                        return false;
                    }
                    else
                    {
                        var newMap = new ExtendedMap(this.map, find.Boxs);

                        PointToPointPathFinder pathFinder = new PointToPointPathFinder(newMap, find.Kaku);

                        if (pathFinder.HasPath(state.Kaku)) return false;
                    }
                }
            }
            return true;
        }
    }

    public class KakuBoxMovingFinder : MovingPathFinder<KakuBoxState>
    {
        public KakuBoxMovingFinder(Map map, KakuBoxState start)
            : base(map, start)
        {
            var p1 = new MapCheckPolicy(this.map);
            var p2 = new OverlappingCheckPolicy();
            var p4 = new KakuMovingDuplicationsDetectPolicy(this.map, this.queue);
            var p3 = new KakuMovingCheckPolicy(this.map);


            this.checker.AddPolicy(p1);
            this.checker.AddPolicy(p2);
            this.checker.AddPolicy(p3);
            this.checker.AddPolicy(p4);
        }
    }
}
