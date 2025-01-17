using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ClipperLib1;
using geoWrangler;

namespace Variance;

using Path = List<IntPoint>;
using Paths = List<List<IntPoint>>;

internal class ChaosEngine
{
    private Paths listOfOutputPoints;
    public Paths getPaths()
    {
        return pGetPaths();
    }

    private Paths pGetPaths()
    {
        return listOfOutputPoints;
    }

    private string result;
    public string getResult()
    {
        return pGetResult();
    }

    private string pGetResult()
    {
        return result;
    }

    private bool outputValid;
    public bool isValid()
    {
        return pIsValid();
    }

    private bool pIsValid()
    {
        return outputValid;
    }

    private List<PreviewShape> simShapes;
    private Paths[] booleanPaths;
    private bool[] inputLayerEnabled;

    private List<Paths> preFlight(Paths aPath, Paths bPath)
    {
        // Put 0-index point at minX (see method for more notes)
        int aCount = aPath.Count;
#if !CHAOSSINGLETHREADED
        Parallel.For(0, aCount, path =>
#else
            for (Int32 path = 0; path < aCount; path++)
#endif
            {
                aPath[path] = reOrderPath("A", path);
            }
#if !CHAOSSINGLETHREADED
        );
#endif
        // Put 0-index point at minY (see method for more notes)
        int bCount = bPath.Count;
#if !CHAOSSINGLETHREADED
        Parallel.For(0, bCount, path =>
#else
            for (Int32 path = 0; path < bCount; path++)
#endif
            {
                bPath[path] = reOrderPath("B", path);
            }
#if !CHAOSSINGLETHREADED
        );
#endif
        List<Paths> returnPaths = new() {aPath.ToList(), bPath.ToList()};

        return returnPaths;
    }

    private Path reOrderPath(string shapeRef, int pathIndex)
    {
        if (shapeRef.ToUpper() != "A" && shapeRef.ToUpper() != "B")
        {
            // Bad callsite. Throw exception.
            throw new Exception("reOrderPath: No shapeRef supplied!");
        }

        Path sourcePath = shapeRef.ToUpper() == "A" ? booleanPaths[0][pathIndex].ToList() : booleanPaths[1][pathIndex].ToList();

        Path returnPath = GeoWrangler.clockwiseAndReorder(sourcePath);
        return returnPath;
    }

    public static Paths customBoolean(int firstLayerOperator, Paths firstLayer, int secondLayerOperator, Paths secondLayer, int booleanFlag, double resolution, double extension)
    {
        return pCustomBoolean(firstLayerOperator, firstLayer, secondLayerOperator, secondLayer, booleanFlag, resolution, extension);
    }

    private static Paths pCustomBoolean(int firstLayerOperator, Paths firstLayer, int secondLayerOperator, Paths secondLayer, int booleanFlag, double resolution, double extension)
    {
        // In principle, 'rigorous' handling is only needed where the cutter is fully enclosed by the subject polygon.
        // The challenge is to know whether this is the case or not.
        // Possibility would be an intersection test and a vertex count and location comparison from before and after, to see whether anything changed.
        bool rigorous = GeoWrangler.enclosed(firstLayer, secondLayer); // this is not a strict check because the enclosure can exist either way for this situation.

        // Need a secondary check because keyholed geometry could be problematic.
        // Both paths will be reviewed; first one to have a keyhole will trigger the rigorous process.
        if (!rigorous)
        {
            try
            {
                rigorous = GeoWrangler.enclosed(firstLayer, customSizing: 1, extension: extension, strict: true); // force a strict check.

                if (!rigorous)
                {
                    // Need a further check because keyholed geometry in B could be problematic.
                    rigorous = GeoWrangler.enclosed(secondLayer, customSizing: 1, extension: extension, strict: true); // force a strict check.
                }
            }
            catch (Exception)
            {
                // No big deal - carry on.
            }
        }

        Paths ret = layerBoolean(firstLayerOperator, firstLayer, secondLayerOperator, secondLayer, booleanFlag, preserveColinear: true);

        ret = GeoWrangler.gapRemoval(ret, extension: extension).ToList();

        bool holes = false;

        foreach (Path t in ret)
        {
            holes = !Clipper.Orientation(t);
            bool gwHoles = !GeoWrangler.isClockwise(t);
            if (holes != gwHoles)
            {
            }
            if (holes)
            {
                break;
            }
        }

        // Apply the keyholing and rationalize.
        if (holes)
        {
            Fragmenter f = new(resolution * CentralProperties.scaleFactorForOperation);
            ret = f.fragmentPaths(ret);
            Paths merged = GeoWrangler.makeKeyHole(ret, extension:extension);

            int count = merged.Count;
#if !CHAOSSINGLETHREADED
            Parallel.For(0, count, i =>
#else
                for (int i = 0; i < count; i++)
#endif
                {
                    merged[i] = GeoWrangler.clockwise(merged[i]);
                }
#if !CHAOSSINGLETHREADED
            );
#endif
            // Squash any accidental keyholes - not ideal, but best option found so far.
            Clipper c1 = new() {PreserveCollinear = true};
            c1.AddPaths(merged, PolyType.ptSubject, true);
            c1.Execute(ClipType.ctUnion, ret);
            ret = GeoWrangler.stripColinear(ret, 1.0);
        }

        ret = GeoWrangler.sliverRemoval(ret, extension: extension); // experimental to try and remove any slivers.

        if (rigorous && !holes)
        {
            int count = ret.Count;
#if !CHAOSSINGLETHREADED
            Parallel.For(0, count, i =>
#else
                for (int i = 0; i < count; i++)
#endif
                {
                    ret[i] = GeoWrangler.clockwise(ret[i]);
                    ret[i] = GeoWrangler.close(ret[i]);
                }
#if !CHAOSSINGLETHREADED
            );
#endif
            // Return here because the attempt to rationalize the geometry below also screws things up, it seems.
            return GeoWrangler.stripColinear(ret, 1.0);
        }

        IntRect bounds = ClipperBase.GetBounds(ret);

        Path bound = new()
        {
            new IntPoint(bounds.left, bounds.bottom),
            new IntPoint(bounds.left, bounds.top),
            new IntPoint(bounds.right, bounds.top),
            new IntPoint(bounds.right, bounds.bottom),
            new IntPoint(bounds.left, bounds.bottom)
        };

        Clipper c = new();

        c.AddPaths(ret, PolyType.ptSubject, true);
        c.AddPath(bound, PolyType.ptClip, true);

        Paths simple = new();
        c.Execute(ClipType.ctIntersection, simple);

        return GeoWrangler.clockwiseAndReorder(simple);
    }

    private Paths layerBoolean(EntropySettings simulationSettings, int firstLayer, int secondLayer, int booleanFlag, bool preserveColinear = true)
    {
        Paths firstLayerPaths = GeoWrangler.pathsFromPointFs(simShapes[firstLayer].getPoints(), CentralProperties.scaleFactorForOperation);

        Paths secondLayerPaths = GeoWrangler.pathsFromPointFs(simShapes[secondLayer].getPoints(), CentralProperties.scaleFactorForOperation);

        return layerBoolean(simulationSettings.getOperatorValue(EntropySettings.properties_o.layer, firstLayer), firstLayerPaths,
            simulationSettings.getOperatorValue(EntropySettings.properties_o.layer, secondLayer), secondLayerPaths, booleanFlag, preserveColinear);
    }

    private static Paths layerBoolean(int firstLayerOperator, Paths firstLayerPaths, int secondLayerOperator, Paths secondLayerPaths, int booleanFlag, bool preserveColinear)
    {
        if (firstLayerOperator == 1) // NOT layer handling
        {
            try
            {
                firstLayerPaths = GeoWrangler.invertTone(firstLayerPaths).ToList();
            }
            catch (Exception)
            {
                // Something blew up.
            }
            firstLayerPaths[0] = GeoWrangler.close(firstLayerPaths[0]);
        }


        if (secondLayerOperator == 1) // NOT layer handling
        {
            try
            {
                secondLayerPaths = GeoWrangler.invertTone(secondLayerPaths).ToList();
            }
            catch (Exception)
            {
                // Something blew up.
            }
            secondLayerPaths[0] = GeoWrangler.close(secondLayerPaths[0]);
        }

        if (firstLayerPaths[0].Count <= 1)
        {
            return secondLayerPaths.ToList();
        }
        return secondLayerPaths[0].Count <= 1 ? firstLayerPaths.ToList() : layerBoolean(firstLayerPaths, secondLayerPaths, booleanFlag, preserveColinear: preserveColinear);
    }

    private static Paths layerBoolean(Paths firstPaths, Paths secondPaths, int booleanFlag, bool preserveColinear = true)
    {
        string booleanType = "AND";
        if (booleanFlag == 1)
        {
            booleanType = "OR";
        }

        // important - if we don't do this, we lose the fragmentation on straight edges.
        Clipper c = new() {PreserveCollinear = preserveColinear};

        c.AddPaths(firstPaths, PolyType.ptSubject, true);
        c.AddPaths(secondPaths, PolyType.ptClip, true);

        Paths outputPoints = new();

        switch (booleanType)
        {
            case "AND":
                c.Execute(ClipType.ctIntersection, outputPoints, PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);
                break;
            case "OR":
                c.Execute(ClipType.ctUnion, outputPoints, PolyFillType.pftEvenOdd, PolyFillType.pftEvenOdd);
                break;
        }

        return outputPoints; // Return our first list of points as the result of the boolean.
    }

    private Paths[] layerBoolean(CommonVars commonVars, bool preserveColinear = true)
    {
        // Boolean is structured as:
        // Process two layers to get the interaction of two layers.
        // Process each pair of results for the output of 4 layers
        // Take each pair of results and get the combination of 8 layers.

        EntropySettings simulationSettings = commonVars.getSimulationSettings();

        int limit2 = simulationSettings.getOperator(EntropySettings.properties_o.twoLayer).Length;
        int limit4 = limit2 / 2;
        int limit8 = limit4 / 2;

        Paths[] twoLayerResults = new Paths[limit2];
        Paths[] fourLayerResults = new Paths[limit4];
        Paths[] eightLayerResults = new Paths[limit8];

        Path tPath = new();

#if !CHAOSSINGLETHREADED
        Parallel.For(0, limit2, i =>
#else
            for (int i = 0; i < limit2; i++)
#endif
            {
                switch (inputLayerEnabled[i * 2])
                {
                    case true when inputLayerEnabled[i * 2 + 1]:
                        twoLayerResults[i] = layerBoolean(simulationSettings, i * 2, i * 2 + 1, simulationSettings.getOperatorValue(EntropySettings.properties_o.twoLayer, i), preserveColinear: preserveColinear).ToList();
                        break;
                    case true when !inputLayerEnabled[i * 2 + 1]:
                        twoLayerResults[i] = layerBoolean(simulationSettings, i * 2, i * 2, 0, preserveColinear: preserveColinear);
                        break;
                    case false when inputLayerEnabled[i * 2 + 1]:
                        twoLayerResults[i] = layerBoolean(simulationSettings, i * 2 + 1, i * 2 + 1, 0, preserveColinear: preserveColinear);
                        break;
                    default:
                        twoLayerResults[i] = new Paths();
                        break;
                }
            }
#if !CHAOSSINGLETHREADED
        );
#endif
        /* Direct the 4 layer boolean approach
         -2 : no active layers.
         -1 : only the left layer is enabled.
          0 : both layers are enabled.
          1 : only the right layer is enabled.
         */
        int[] doLayer4Boolean = new int[limit4];

#if !CHAOSSINGLETHREADED
        Parallel.For(0, limit4, i =>
#else
            for (int i = 0; i < limit4; i++)
#endif
            {
                if (
                    (inputLayerEnabled[i * 4] || inputLayerEnabled[i * 4 + 1]) &&
                    (inputLayerEnabled[i * 4 + 2] || inputLayerEnabled[i * 4 + 3])
                )
                {
                    doLayer4Boolean[i] = 0;
                }
                else
                {
                    if (inputLayerEnabled[i * 4] || inputLayerEnabled[i * 4 + 1])
                    {
                        doLayer4Boolean[i] = -1;
                    }
                    else if (inputLayerEnabled[i * 4 + 2] || inputLayerEnabled[i * 4 + 3])
                    {
                        doLayer4Boolean[i] = 1;
                    }
                    else
                    {
                        doLayer4Boolean[i] = -2;
                    }
                }
            }
#if !CHAOSSINGLETHREADED
        );
#endif
#if !CHAOSSINGLETHREADED
        Parallel.For(0, limit4, i =>
#else
            for (int i = 0; i < limit4; i++)
#endif
            {
                if (doLayer4Boolean[i] == 0 && twoLayerResults[i * 2].Count > 0 && twoLayerResults[i * 2 + 1].Count > 0)
                {
                    fourLayerResults[i] = layerBoolean(
                        firstPaths: twoLayerResults[i * 2],
                        secondPaths: twoLayerResults[i * 2 + 1],
                        booleanFlag: simulationSettings.getOperatorValue(EntropySettings.properties_o.fourLayer, i),
                        preserveColinear: preserveColinear
                    ).ToList();
                }
                else
                {
                    switch (doLayer4Boolean[i])
                    {
                        case -1:
                            fourLayerResults[i] = twoLayerResults[i * 2].ToList();
                            break;
                        case 1:
                            fourLayerResults[i] = twoLayerResults[i * 2 + 1].ToList();
                            break;
                        case 0:
                            if (twoLayerResults[i * 2].Count > 0)
                            {
                                if (simulationSettings.getOperatorValue(EntropySettings.properties_o.fourLayer, i) == 0)
                                {
                                    fourLayerResults[i] = new Paths();
                                }
                                else
                                {
                                    fourLayerResults[i] = twoLayerResults[i * 2].ToList();
                                }
                            }
                            else if (twoLayerResults[i * 2 + 1].Count > 0)
                            {
                                if (simulationSettings.getOperatorValue(EntropySettings.properties_o.fourLayer, i) == 0)
                                {
                                    fourLayerResults[i] = new Paths();
                                }
                                else
                                {
                                    fourLayerResults[i] = twoLayerResults[i * 2 + 1].ToList();
                                }
                            }
                            else
                            {
                                fourLayerResults[i] = new Paths();
                            }
                            break;
                        default:
                            fourLayerResults[i] = new Paths();
                            break;
                    }
                }
            }
#if !CHAOSSINGLETHREADED
        );
#endif
        /* Direct the 8 layer boolean approach
         -2 : no active layers.
         -1 : only the left layer is enabled.
          0 : both layers are enabled.
          1 : only the right layer is enabled.
         */
        int[] doLayer8Boolean = new int[limit8];
            
#if !CHAOSSINGLETHREADED
        Parallel.For(0, limit8, i =>
#else
            for (int i = 0; i < limit8; i++)
#endif
            {
                // Are both sides active?
                // 0th loop : 0   1        5
                // next     : 8   9
                // next     : 16  17
                if (
                    (
                        inputLayerEnabled[i * 8] || inputLayerEnabled[i * 8 + 1] || inputLayerEnabled[i * 8 + 2] || inputLayerEnabled[i * 8 + 3]
                    ) &
                    (
                        inputLayerEnabled[i * 8 + 4] || inputLayerEnabled[i * 8 + 5] || inputLayerEnabled[i * 8 + 6] || inputLayerEnabled[i * 8 + 7]
                    )
                )
                {
                    doLayer8Boolean[i] = 0;
                }
                else
                {
                    if (
                        inputLayerEnabled[i * 8] || inputLayerEnabled[i * 8 + 1] || inputLayerEnabled[i * 8 + 2] || inputLayerEnabled[i * 8 + 3]
                    )
                    {
                        doLayer8Boolean[i] = -1;
                    }
                    else if (
                        inputLayerEnabled[i * 8 + 4] || inputLayerEnabled[i * 8 + 5] || inputLayerEnabled[i * 8 + 6] || inputLayerEnabled[i * 8 + 7]
                    )
                    {
                        doLayer8Boolean[i] = 1;
                    }
                    else
                    {
                        doLayer8Boolean[i] = -2;
                    }
                }
            }
#if !CHAOSSINGLETHREADED
        );
#endif

#if !CHAOSSINGLETHREADED
        Parallel.For(0, limit8, i =>
#else
            for (int i = 0; i < limit8; i++)
#endif
            {
                if (doLayer8Boolean[i] == 0 && fourLayerResults[i * 2].Count > 0 && fourLayerResults[i * 2 + 1].Count > 0)
                {
                    eightLayerResults[i] = layerBoolean(
                        firstPaths: fourLayerResults[i * 2],
                        secondPaths: fourLayerResults[i * 2 + 1],
                        booleanFlag: simulationSettings.getOperatorValue(EntropySettings.properties_o.eightLayer, i),
                        preserveColinear: preserveColinear
                    ).ToList();
                }
                else
                {
                    switch (doLayer8Boolean[i])
                    {
                        case -1:
                            eightLayerResults[i] = fourLayerResults[i * 2].ToList();
                            break;
                        case 1:
                            eightLayerResults[i] = fourLayerResults[i * 2 + 1].ToList();
                            break;
                        case 0:
                            if (fourLayerResults[i * 2].Count > 0)
                            {
                                if (simulationSettings.getOperatorValue(EntropySettings.properties_o.eightLayer, i) == 0)
                                {
                                    eightLayerResults[i] = new Paths {tPath};
                                }
                                else
                                {
                                    eightLayerResults[i] = fourLayerResults[i * 2].ToList();
                                }
                            }
                            else if (fourLayerResults[i * 2 + 1].Count > 0)
                            {
                                if (simulationSettings.getOperatorValue(EntropySettings.properties_o.eightLayer, i) == 0)
                                {
                                    eightLayerResults[i] = new Paths {tPath};
                                }
                                else
                                {
                                    eightLayerResults[i] = fourLayerResults[i * 2 + 1].ToList();
                                }
                            }
                            else
                            {
                                eightLayerResults[i] = new Paths {tPath};
                            }
                            break;
                        default:
                            eightLayerResults[i] = new Paths {tPath};
                            break;
                    }
                }
            }
#if !CHAOSSINGLETHREADED
        );
#endif
        return eightLayerResults;
    }

    // Preview mode is intended to allow multi-threaded evaluation for a single case - batch calculations run multiple separate single-threaded evaluations
    public ChaosEngine(CommonVars commonVars, List<PreviewShape> simShapes_, bool previewMode)
    {
        pChaosEngine(commonVars, simShapes_, previewMode);
    }

    private void pChaosEngine(CommonVars commonVars, List<PreviewShape> simShapes_, bool previewMode)
    {
        outputValid = false;
        simShapes = simShapes_;

        listOfOutputPoints = new Paths();

        EntropySettings simulationSettings = commonVars.getSimulationSettings();

        bool sgRemove_a = false;
        bool sgRemove_b = false;

        inputLayerEnabled = new bool[CentralProperties.maxLayersForMC];
        for (int i = 0; i < CentralProperties.maxLayersForMC; i++)
        {
            inputLayerEnabled[i] = commonVars.getLayerSettings(i).getInt(EntropyLayerSettings.properties_i.enabled) == 1;
            // Modify our state based on the omit flag (in case this layer is being used by an in-layer boolean elsewhere and the user requested to omit the input layer.
            inputLayerEnabled[i] = inputLayerEnabled[i] && commonVars.getLayerSettings(i).getInt(EntropyLayerSettings.properties_i.omit) == 0;
            if (!sgRemove_a)
            {
                if (i < CentralProperties.maxLayersForMC / 2)
                {
                    sgRemove_a = commonVars.getLayerSettings(i).getInt(EntropyLayerSettings.properties_i.shapeIndex) == (int)CommonVars.shapeNames.BOOLEAN;
                }
            }

            if (sgRemove_b)
            {
                continue;
            }

            if (i >= CentralProperties.maxLayersForMC / 2)
            {
                sgRemove_b = commonVars.getLayerSettings(i).getInt(EntropyLayerSettings.properties_i.shapeIndex) == (int)CommonVars.shapeNames.BOOLEAN;
            }
        }

        bool preserveColinear = commonVars.getSimulationSettings().getValue(EntropySettings.properties_i.oType) == (int)CommonVars.calcModes.enclosure_spacing_overlap;

        booleanPaths = layerBoolean(commonVars, preserveColinear);

        if (sgRemove_a)
        {
            booleanPaths[0] = GeoWrangler.sliverGapRemoval(booleanPaths[0]);
        }
        if (sgRemove_b)
        {
            booleanPaths[1] = GeoWrangler.sliverGapRemoval(booleanPaths[1]);
        }

        int layerAPathCount_orig = booleanPaths[0].Count;
        int layerBPathCount_orig = booleanPaths[1].Count;

        // Let's validate that we have something reasonable for the inputs before we do something with them.
        bool inputsValid = !(layerAPathCount_orig == 0 || layerBPathCount_orig == 0);

        if (inputsValid)
        {
            switch (simulationSettings.getValue(EntropySettings.properties_i.oType))
            {
                case (int)CommonVars.calcModes.area: // area
                    try
                    {
                        bool perPoly = simulationSettings.getValue(EntropySettings.properties_i.subMode) == (int)CommonVars.areaCalcModes.perpoly;
                        AreaHandler aH = new(aPaths: booleanPaths[0], bPaths: booleanPaths[1], maySimplify: true, perPoly);
                        // Sum the areas by polygon returned.
                        result = (Convert.ToDouble(result) + aH.area).ToString(CultureInfo.InvariantCulture);
                        listOfOutputPoints.AddRange(aH.listOfOutputPoints);
                        outputValid = true;
                    }
                    catch (Exception)
                    {
                        // rejected case - don't care.
                    }
                    break;

                case (int)CommonVars.calcModes.enclosure_spacing_overlap: // spacing (or enclosure)
                    DistanceHandler dH = new(aPaths: booleanPaths[0], bPaths: booleanPaths[1], simulationSettings, previewMode); // in preview mode, raycaster inside this engine will run threaded along the emit edge.

                    // Store minimum case for the per polygon system.
                    if (result == null)
                    {
                        result = dH.distanceString;
                        listOfOutputPoints = dH.resultPaths.ToList();
                    }
                    else
                    {
                        // Overlaps are reported as negative values, so this will handle both spacing and overlap cases.
                        if (Convert.ToDouble(dH.distanceString) < Convert.ToDouble(result))
                        {
                            result = dH.distanceString;
                            listOfOutputPoints = dH.resultPaths.ToList();
                        }
                    }

                    // Viewport needs a polygon - lines aren't handled properly, so let's double up our line.
                    try
                    {
                        foreach (Path t in listOfOutputPoints)
                        {
                            int pt = t.Count - 1;
                            while (pt > 0)
                            {
                                t.Add(new IntPoint(t[pt]));
                                pt--;
                            }
                        }
                    }
                    catch (Exception)
                    {

                    }
                    outputValid = true;

                    break;

                case (int)CommonVars.calcModes.chord: // chord
                    // ReSharper disable once ConvertIfStatementToNullCoalescingAssignment
                    if (result == null)
                    {
                        result = "0.0,0.0,0.0,0.0";
                    }

                    double[] fraggedResult = new double[4];
                    fraggedResult[0] = fraggedResult[1] = fraggedResult[2] = fraggedResult[3] = 0.0;

                    Path tmpPath = new() {new IntPoint(0, 0)};
                    listOfOutputPoints.Add(tmpPath.ToList());
                    listOfOutputPoints.Add(tmpPath.ToList());
                    listOfOutputPoints.Add(tmpPath.ToList());
                    listOfOutputPoints.Add(tmpPath.ToList());

                    try
                    {
                        Paths aPath = booleanPaths[0].ToList();
                        Paths bPath = booleanPaths[1].ToList();
                        List<Paths> cleanedPaths = preFlight(aPath, bPath).ToList();
                        aPath = cleanedPaths[0].ToList();
                        bPath = cleanedPaths[1].ToList();
                        ChordHandler cH = new(aPath, bPath, simulationSettings);

                        // Fragment our result.
                        char[] resultSeparators = { ',' }; // CSV separator for splitting results for comparison.
                        string[] tmpfraggedResult = result.Split(resultSeparators);
#if !CHAOSSINGLETHREADED
                        Parallel.For(0, tmpfraggedResult.Length, i =>
#else
                            for (Int32 i = 0; i < tmpfraggedResult.Length; i++)
#endif
                            {
                                fraggedResult[i] = Convert.ToDouble(tmpfraggedResult[i]);
                            }
#if !CHAOSSINGLETHREADED
                        );
#endif
                        fraggedResult[0] = cH.aChordLengths[0] / CentralProperties.scaleFactorForOperation;
                        listOfOutputPoints[0] = cH.a_chordPaths[0].ToList();
                        fraggedResult[1] = cH.aChordLengths[1] / CentralProperties.scaleFactorForOperation;
                        listOfOutputPoints[1] = cH.a_chordPaths[1].ToList();
                        fraggedResult[2] = cH.bChordLengths[0] / CentralProperties.scaleFactorForOperation;
                        listOfOutputPoints[2] = cH.b_chordPaths[0].ToList();
                        fraggedResult[3] = cH.bChordLengths[1] / CentralProperties.scaleFactorForOperation;
                        listOfOutputPoints[3] = cH.b_chordPaths[1].ToList();

                        if (simulationSettings.getValue(EntropySettings.properties_i.subMode) != (int)CommonVars.chordCalcElements.b)
                        {
                            result = fraggedResult[0] + "," + fraggedResult[1];
                        }
                        else
                        {
                            result = "N/A,N/A";
                        }

                        if (simulationSettings.getValue(EntropySettings.properties_i.subMode) >= (int)CommonVars.chordCalcElements.b)
                        {
                            result += "," + fraggedResult[2] + "," + fraggedResult[3];
                        }
                        else
                        {
                            result += ",N/A,N/A";
                        }
                        outputValid = true;
                    }
                    catch (Exception)
                    {
                        // We don't care about exceptions - these are probably rejected cases from coincident edges.
                    }
                    break;

                case (int)CommonVars.calcModes.angle: // angle
                    for (int layerAPoly = 0; layerAPoly < layerAPathCount_orig; layerAPoly++)
                    {
                        for (int layerBPoly = 0; layerBPoly < layerBPathCount_orig; layerBPoly++)
                        {
                            try
                            {
                                angleHandler agH = new(layerAPath: booleanPaths[0], layerBPath: booleanPaths[1]);
                                if (result == null)
                                {
                                    result = agH.minimumIntersectionAngle.ToString(CultureInfo.InvariantCulture);
                                    listOfOutputPoints = agH.resultPaths.ToList();
                                }
                                else
                                {
                                    if (agH.minimumIntersectionAngle < Convert.ToDouble(result))
                                    {
                                        result = agH.minimumIntersectionAngle.ToString(CultureInfo.InvariantCulture);
                                        listOfOutputPoints.Clear();
                                        listOfOutputPoints = agH.resultPaths.ToList();
                                    }
                                }
                                outputValid = true; // mark that we're good for the callsite
                            }
                            catch (Exception)
                            {
                                // rejected case.
                            }
                        }
                    }
                    break;
            }
        }
        else
        {
            outputValid = false;
            // Need to return empty values.
        }
    }
}