using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections;
using System.Diagnostics;

namespace KakuBoxMoving.Optimized
//namespace KakuBoxMoving
{
    class Program
    {
        public static bool parallel = true;
        static void Main(string[] args)
        {
            Map map = new Map(new Point[] { new Point(0, 0), new Point(0, 1), new Point(3, 0), new Point(4, 0), new Point(5, 0), new Point(1, 3)
            , new Point(3, 2), new Point(3, 4)});
            map.XDownBound = 0;
            map.XUpBound = 5;
            map.YDownBound = 0;
            map.YUpBound = 4;

            KakuBoxState start = new KakuBoxState(new Point(5, 4), new Point[] { new Point(1, 2), new Point(3, 3), new Point(4, 2) });
            BoxState end = new BoxState(new Point[] { new Point(2, 1), new Point(2, 2), new Point(2, 4) });

            KakuBoxMovingFinder finder = new KakuBoxMovingFinder(map, start);

            Stopwatch sw = new Stopwatch();

            sw.Start();

            var path = finder.FindPath(end);

            sw.Stop();

            if (path.Count > 0)
            {
                Console.WriteLine("Find it!");

                foreach(var state in path)
                {
                    Console.WriteLine(state.ToString());
                }
            }
            else
            {
                Console.WriteLine("No answer. Are you kidding me!");
            }
            Console.WriteLine("Total used time:{0}", sw.Elapsed.TotalSeconds);
            Console.ReadLine();
        }
    }
}


