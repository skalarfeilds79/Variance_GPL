using ClipperLib;
using geoWrangler;
using System;
using System.Collections.Generic;
using System.Linq;
using utility;

namespace Variance
{
    using Path = List<IntPoint>;
    using Paths = List<List<IntPoint>>;

    class angleHandler
    {
        public double minimumIntersectionAngle { get; set; }
        Paths listOfOutputPoints;
        public Paths resultPaths { get; set; } // will only have one path, for minimum angle.

        void ZFillCallback(IntPoint bot1, IntPoint top1, IntPoint bot2, IntPoint top2, ref IntPoint pt)
        {
            pt.Z = -1; // Tag our intersection points.
        }

        // Distance functions to drive scale-up of intersection marker if needed.
        double minDistance = 10.0;

        public angleHandler(Paths layerAPath, Paths layerBPath)
        {
            angleHandlerLogic(layerAPath, layerBPath);
        }

        void angleHandlerLogic(Paths layerAPath, Paths layerBPath)
        {
            listOfOutputPoints = new Paths();
            resultPaths = new Paths();
            Path resultPath = new Path();
            Clipper c = new Clipper();
            c.ZFillFunction = ZFillCallback;
            List<string> stringList = new List<string>();
            c.AddPaths(layerAPath, PolyType.ptSubject, true);
            c.AddPaths(layerBPath, PolyType.ptClip, true);

            // Boolean AND of the two levels for the area operation.
            c.Execute(ClipType.ctIntersection, listOfOutputPoints);

            // Set initial output value for the case there are no intersections
            minimumIntersectionAngle = 180.0; // no intersection angle.

            double tmpVal = 0.0;
            for (Int32 i = 0; i < listOfOutputPoints.Count(); i++)
            {
                tmpVal += Clipper.Area(listOfOutputPoints[i]);
            }
            if (tmpVal == 0.0)
            {
                // No overlap
                // Set output path and avoid heavy lifting
                resultPath.Add(new IntPoint(0, 0));
                resultPaths.Add(resultPath);
            }
            else
            {
                double temporaryResult = 180.0;
                Path temporaryPath = new Path();
                temporaryPath.Add(new IntPoint(0, 0));
                temporaryPath.Add(new IntPoint(0, 0));
                temporaryPath.Add(new IntPoint(0, 0));
                for (Int32 path = 0; path < listOfOutputPoints.Count(); path++)
                {
                    Path overlapPath = GeoWrangler.clockwise(listOfOutputPoints[path]);

                    int pt = 0;
                    while (pt < overlapPath.Count())
                    {
                        if (overlapPath[pt].Z == -1)
                        {
                            // intersection point found - let's get our three points to find the angle.
                            // http://en.wikipedia.org/wiki/Law_of_cosines
                            IntPoint interSection_B;
                            IntPoint interSection_C;
                            IntPoint interSection_A;
                            if (pt == 0)
                            {
                                // Find preceding not-identical point.
                                int refPt = overlapPath.Count - 1;
                                while (Math.Abs(GeoWrangler.distanceBetweenPoints(overlapPath[refPt], overlapPath[pt])) == 0)
                                {
                                    refPt--;
                                    if (refPt == 0)
                                    {
                                        break;
                                    }
                                }
                                interSection_B = overlapPath[refPt]; // map to last point
                                interSection_C = overlapPath[pt];
                                // Find following not-identical point.
                                refPt = 0;
                                while (Math.Abs(GeoWrangler.distanceBetweenPoints(overlapPath[refPt], overlapPath[pt])) == 0)
                                {
                                    refPt++;
                                    if (refPt == overlapPath.Count - 1)
                                    {
                                        break;
                                    }
                                }
                                interSection_A = overlapPath[refPt];
                            }
                            else if (pt == overlapPath.Count() - 1) // last point in the list
                            {
                                // Find preceding not-identical point.
                                int refPt = pt;
                                while (Math.Abs(GeoWrangler.distanceBetweenPoints(overlapPath[refPt], overlapPath[pt])) == 0)
                                {
                                    refPt--;
                                    if (refPt == 0)
                                    {
                                        break;
                                    }
                                }
                                interSection_B = overlapPath[refPt];
                                interSection_C = overlapPath[pt];
                                // Find following not-identical point.
                                refPt = 0;
                                while (Math.Abs(GeoWrangler.distanceBetweenPoints(overlapPath[refPt], overlapPath[pt])) == 0)
                                {
                                    refPt++;
                                    if (refPt == overlapPath.Count - 1)
                                    {
                                        break;
                                    }
                                }
                                interSection_A = overlapPath[0]; // map to the first point
                            }
                            else
                            {
                                // Find preceding not-identical point.
                                int refPt = pt;
                                while (Math.Abs(GeoWrangler.distanceBetweenPoints(overlapPath[refPt], overlapPath[pt])) == 0)
                                {
                                    refPt--;
                                    if (refPt == 0)
                                    {
                                        break;
                                    }
                                }
                                interSection_B = overlapPath[refPt];
                                interSection_C = overlapPath[pt];
                                // Find following not-identical point.
                                refPt = pt;
                                while (Math.Abs(GeoWrangler.distanceBetweenPoints(overlapPath[refPt], overlapPath[pt])) == 0)
                                {
                                    refPt++;
                                    if (refPt == overlapPath.Count - 1)
                                    {
                                        break;
                                    }
                                }
                                interSection_A = overlapPath[refPt];
                            }

                            IntPoint cBVector = new IntPoint(interSection_B.X - interSection_C.X, interSection_B.Y - interSection_C.Y);
                            IntPoint cAVector = new IntPoint(interSection_A.X - interSection_C.X, interSection_A.Y - interSection_C.Y);

                            Int64 xComponents = cBVector.X * cAVector.X;
                            Int64 yComponents = cBVector.Y * cAVector.Y;

                            Int64 scalarProduct = xComponents + yComponents;

                            double cBMagnitude = (Math.Sqrt(Utils.myPow(cBVector.X, 2) + Utils.myPow(cBVector.Y, 2)));
                            double cAMagnitude = (Math.Sqrt(Utils.myPow(cAVector.X, 2) + Utils.myPow(cAVector.Y, 2)));

                            double theta = Math.Abs(Utils.toDegrees(Math.Acos((scalarProduct) / (cBMagnitude * cAMagnitude)))); // Avoid falling into a trap with negative angles.

                            if (theta < temporaryResult)
                            {
                                temporaryResult = theta;
                                temporaryPath.Clear();
                                temporaryPath.Add(new IntPoint(interSection_A.X, interSection_A.Y));
                                temporaryPath.Add(new IntPoint(interSection_C.X, interSection_C.Y));
                                temporaryPath.Add(new IntPoint(interSection_B.X, interSection_B.Y));
                            }
                        }
                        pt++;
                    }
                }
                minimumIntersectionAngle = temporaryResult;

                // Check our temporary path to see if we need to scale it up.
                double distance = GeoWrangler.distanceBetweenPoints(temporaryPath[0], temporaryPath[1]) / CentralProperties.scaleFactorForOperation;
                IntPoint distanceIntPoint = GeoWrangler.intPoint_distanceBetweenPoints(temporaryPath[0], temporaryPath[1]); // A to C
                if (distance < minDistance)
                {
                    double X = temporaryPath[0].X;
                    double Y = temporaryPath[0].Y;
                    if (temporaryPath[1].X != temporaryPath[0].X)
                    {
                        X = temporaryPath[1].X + (distanceIntPoint.X * (minDistance / distance));
                    }
                    if (temporaryPath[1].Y != temporaryPath[0].Y)
                    {
                        Y = temporaryPath[1].Y + (distanceIntPoint.Y * (minDistance / distance));
                    }
                    temporaryPath[0] = new IntPoint((Int64)X, (Int64)Y);
                }
                distance = GeoWrangler.distanceBetweenPoints(temporaryPath[2], temporaryPath[1]) / CentralProperties.scaleFactorForOperation;
                distanceIntPoint = GeoWrangler.intPoint_distanceBetweenPoints(temporaryPath[2], temporaryPath[1]); // B to C
                if (distance < minDistance)
                {
                    double X = temporaryPath[2].X;
                    double Y = temporaryPath[2].Y;
                    if (temporaryPath[1].Y != temporaryPath[2].Y)
                    {
                        Y = temporaryPath[1].Y + (distanceIntPoint.Y * (minDistance / distance));
                    }
                    if (temporaryPath[1].X != temporaryPath[2].X)
                    {
                        X = temporaryPath[1].X + (distanceIntPoint.X * (minDistance / distance));
                    }
                    temporaryPath[2] = new IntPoint((Int64)X, (Int64)Y);
                }
                resultPaths.Add(temporaryPath.ToList());
            }
        }
    }
}
