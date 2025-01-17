using System;
using System.Collections.Generic;
using System.Linq;
using ClipperLib1;
using color;
using Error;
using geoLib;
using geoWrangler;
using Noise;
using utility; // tiled layout handling, Layout biasing/CDU.
using System.Threading.Tasks;

namespace Variance;

using Path = List<IntPoint>;
using Paths = List<List<IntPoint>>;

public class PreviewShape
{
    private bool DOEDependency; // due to the DOE grid, we need this to sort out offsets. This includes buried references in Booleans. The min X/Y values for this case need to be at least the col/row offset.

    private Fragmenter fragment;
    // Class for our preview shapes.
    private List<GeoLibPointF[]> previewPoints; // list of polygons defining the shape(s) that will be drawn. In the complex case, we populate this from complexPoints.
    public List<GeoLibPointF[]> getPoints()
    {
        return pGetPoints();
    }

    private List<GeoLibPointF[]> pGetPoints()
    {
        return previewPoints;
    }

    public GeoLibPointF[] getPoints(int index)
    {
        return pGetPoints(index);
    }

    private GeoLibPointF[] pGetPoints(int index)
    {
        return previewPoints[index];
    }

    public void addPoints(GeoLibPointF[] poly)
    {
        pAddPoints(poly);
    }

    private void pAddPoints(GeoLibPointF[] poly)
    {
        previewPoints.Add(poly.ToArray());
    }

    public void setPoints(List<GeoLibPointF[]> newPoints)
    {
        pSetPoints(newPoints);
    }

    private void pSetPoints(List<GeoLibPointF[]> newPoints)
    {
        previewPoints = newPoints.ToList();
    }

    public void clearPoints()
    {
        pClearPoints();
    }

    private void pClearPoints()
    {
        previewPoints.Clear();
    }

    private List<bool> drawnPoly; // to track drawn vs enabled polygons. Can then use for filtering elsewhere.

    public bool getDrawnPoly(int index)
    {
        return pGetDrawnPoly(index);
    }

    private bool pGetDrawnPoly(int index)
    {
        return drawnPoly[index];
    }

    private List<bool> geoCoreOrthogonalPoly;
    private MyColor color;

    public MyColor getColor()
    {
        return pGetColor();
    }

    private MyColor pGetColor()
    {
        return color;
    }

    public void setColor(MyColor c)
    {
        pSetColor(c);
    }

    private void pSetColor(MyColor c)
    {
        color = new MyColor(c);
    }

    private double xOffset;
    private double yOffset;

    private int _settingsIndex; // track originating layer.

    public int getIndex()
    {
        return pGetIndex();
    }

    private int pGetIndex()
    {
        return _settingsIndex;
    }

    private void rectangle_offset(EntropyLayerSettings entropyLayerSettings)
    {
        string posInSubShapeString = ((CommonVars.subShapeLocations)entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.posIndex)).ToString();
        double tmp_xOffset = 0;
        double tmp_yOffset = 0;

        if (posInSubShapeString is "TL" or "TR" or "TS" or "RS" or "LS" or "C")
        {
            // Vertical offset needed to put reference corner at world center
            tmp_yOffset = -Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0VerLength));

            // Half the value for a vertical centering requirement
            if (posInSubShapeString is "RS" or "LS" or "C")
            {
                tmp_yOffset = Convert.ToDouble(tmp_yOffset / 2);
            }
        }
        yOffset -= tmp_yOffset;

        if (posInSubShapeString is "TR" or "BR" or "TS" or "RS" or "BS" or "C")
        {
            tmp_xOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0HorLength));

            // Half the value for horizontal centering conditions
            if (posInSubShapeString is "TS" or "BS" or "C")
            {
                tmp_xOffset = Convert.ToDouble(tmp_xOffset / 2);
            }
        }
        xOffset += tmp_xOffset;
    }

    private void lShape_offset(EntropyLayerSettings entropyLayerSettings)
    {
        string posInSubShapeString = ((CommonVars.subShapeLocations)entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.posIndex)).ToString();
        double tmp_xOffset = 0;
        double tmp_yOffset = 0;

        if (posInSubShapeString is "TL" or "TR" or "TS" or "RS" or "LS" or "C")
        {
            // Vertical offset needed to put reference corner at world center
            if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.subShapeIndex) == 0)
            {
                tmp_yOffset = -Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0VerLength));
            }
            else
            {
                tmp_yOffset = -Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1VerLength));
            }

            // Half the value for a vertical centering requirement
            if (posInSubShapeString is "RS" or "LS" or "C")
            {
                tmp_yOffset = Convert.ToDouble(tmp_yOffset / 2);
            }
        }
        yOffset -= tmp_yOffset;

        if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.subShapeIndex) == 1 && posInSubShapeString is "LS" or "BL" or "TL")
        {
            tmp_xOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0HorLength)); // essentially the same in X as the RS for subshape 1.
        }
        else
        {
            if (posInSubShapeString is "TR" or "BR" or "TS" or "RS" or "BS" or "C")
            {
                if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.subShapeIndex) == 0)
                {
                    tmp_xOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0HorLength));
                }
                else
                {
                    tmp_xOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0HorLength));
                    tmp_xOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1HorLength));
                }

                // Half the value for horizontal centering conditions
                if (posInSubShapeString is "TS" or "BS" or "C")
                {
                    if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.subShapeIndex) == 0)
                    {
                        tmp_xOffset = Convert.ToDouble(tmp_xOffset / 2);
                    }
                    else
                    {
                        tmp_xOffset += Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1HorLength) / 2);
                    }
                }
            }
        }

        xOffset += tmp_xOffset;
    }

    private void tShape_offset(EntropyLayerSettings entropyLayerSettings)
    {
        string posInSubShapeString = ((CommonVars.subShapeLocations)entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.posIndex)).ToString();
        double tmp_xOffset = 0;
        double tmp_yOffset = 0;

        if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.subShapeIndex) == 1 && posInSubShapeString is "BR" or "BL" or "BS")
        {
            tmp_yOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1VerOffset));
        }
        else
        {
            if (posInSubShapeString is "TL" or "TR" or "TS" or "RS" or "LS" or "C")
            {
                if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.subShapeIndex) == 0)
                {
                    tmp_yOffset = -Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0VerLength));
                    // Half the value for a vertical centering requirement
                    if (posInSubShapeString is "RS" or "LS" or "C")
                    {
                        tmp_yOffset = Convert.ToDouble(tmp_yOffset / 2);
                    }
                }
                else
                {
                    tmp_yOffset = -Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1VerLength));
                    // Half the value for a vertical centering requirement
                    if (posInSubShapeString is "RS" or "LS" or "C")
                    {
                        tmp_yOffset = Convert.ToDouble(tmp_yOffset / 2);
                    }
                    tmp_yOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1VerOffset));
                }

            }
        }
        yOffset -= tmp_yOffset;

        if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.subShapeIndex) == 1 && posInSubShapeString is "LS" or "BL" or "TL")
        {
            tmp_xOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0HorLength)); // essentially the same in X as the RS for subshape 1.
        }
        else
        {
            if (posInSubShapeString is "TR" or "BR" or "TS" or "RS" or "BS" or "C")
            {
                if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.subShapeIndex) == 0)
                {
                    tmp_xOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0HorLength));
                }
                else
                {
                    tmp_xOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0HorLength));
                    tmp_xOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1HorLength));
                }

                // Half the value for horizontal centering conditions
                if (posInSubShapeString is "TS" or "BS" or "C")
                {
                    if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.subShapeIndex) == 0)
                    {
                        tmp_xOffset = Convert.ToDouble(tmp_xOffset / 2);
                    }
                    else
                    {
                        tmp_xOffset += Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1HorLength) / 2);
                    }
                }
            }
        }

        xOffset += tmp_xOffset;
    }

    private void xShape_offset(EntropyLayerSettings entropyLayerSettings)
    {
        string posInSubShapeString = ((CommonVars.subShapeLocations)entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.posIndex)).ToString();
        double tmp_xOffset = 0;
        double tmp_yOffset = 0;

        if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.subShapeIndex) == 1 && posInSubShapeString is "BR" or "BL" or "BS")
        {
            tmp_yOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1VerOffset));
        }
        else
        {
            if (posInSubShapeString is "TL" or "TR" or "TS" or "RS" or "LS" or "C")
            {
                // Vertical offset needed to put reference corner at world center
                if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.subShapeIndex) == 0)
                {
                    tmp_yOffset = -Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0VerLength));
                    // Half the value for a vertical centering requirement
                    if (posInSubShapeString is "RS" or "LS" or "C")
                    {
                        tmp_yOffset = Convert.ToDouble(tmp_yOffset / 2);
                    }
                }
                else
                {
                    tmp_yOffset = -Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1VerLength));
                    // Half the value for a vertical centering requirement
                    if (posInSubShapeString is "RS" or "LS" or "C")
                    {
                        tmp_yOffset = Convert.ToDouble(tmp_yOffset / 2);
                    }
                    tmp_yOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1VerOffset));
                }

            }
        }
        yOffset -= tmp_yOffset;

        if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.subShapeIndex) == 1 && posInSubShapeString is "LS" or "BL" or "TL")
        {
            tmp_xOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1HorOffset));
        }
        else
        {
            if (posInSubShapeString is "TR" or "BR" or "TS" or "RS" or "BS" or "C")
            {
                if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.subShapeIndex) == 0)
                {
                    tmp_xOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0HorLength));
                }
                else
                {
                    tmp_xOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1HorLength));
                }

                // Half the value for horizontal centering conditions
                if (posInSubShapeString is "TS" or "BS" or "C")
                {
                    if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.subShapeIndex) == 0)
                    {
                        tmp_xOffset = Convert.ToDouble(tmp_xOffset / 2);
                    }
                    else
                    {
                        tmp_xOffset += Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1HorLength) / 2);
                    }
                }

                if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.subShapeIndex) == 1)
                {
                    tmp_xOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1HorOffset));
                }
            }
        }

        xOffset += tmp_xOffset;
    }

    private void uShape_offset(EntropyLayerSettings entropyLayerSettings)
    {
        string posInSubShapeString = ((CommonVars.subShapeLocations)entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.posIndex)).ToString();
        double tmp_xOffset = 0;
        double tmp_yOffset = 0;

        if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.subShapeIndex) == 0)
        {
            if (posInSubShapeString is "TL" or "TR" or "TS" or "RS" or "LS" or "C")
            {
                tmp_yOffset = -Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0VerLength));

                // Half the value for a vertical centering requirement
                if (posInSubShapeString is "RS" or "LS" or "C")
                {
                    tmp_yOffset = Convert.ToDouble(tmp_yOffset / 2);
                }
            }
            yOffset -= tmp_yOffset;

            if (posInSubShapeString is "TR" or "BR" or "TS" or "RS" or "BS" or "C")
            {
                tmp_xOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0HorLength));

                // Half the value for horizontal centering conditions
                if (posInSubShapeString is "TS" or "BS" or "C")
                {
                    tmp_xOffset = Convert.ToDouble(tmp_xOffset / 2);
                }
            }
        }
        else
        {
            // Subshape 2 is always docked against top edge of subshape 1 in U.
            if (posInSubShapeString is "TL" or "TR" or "TS" or "RS" or "LS" or "BL" or "BR" or "BS" or "C")
            {
                tmp_yOffset = -Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0VerLength));

                switch (posInSubShapeString)
                {
                    // Half the value for a vertical centering requirement
                    case "RS" or "LS" or "C":
                        tmp_yOffset += Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1VerLength) / 2);
                        break;
                    // Subtract the value for a subshape 2 bottom edge requirement
                    case "BL" or "BR" or "BS":
                        tmp_yOffset += Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1VerLength));
                        break;
                }
            }
            yOffset -= tmp_yOffset;

            // Subshape 2 is always H-centered in U. Makes it easy.
            tmp_xOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0HorLength) / 2);

            switch (posInSubShapeString)
            {
                case "TR" or "BR" or "RS":
                    tmp_xOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1HorLength) / 2);
                    break;
                case "TL" or "BL" or "LS":
                    tmp_xOffset += Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1HorLength) / 2);
                    break;
            }
        }
        xOffset += tmp_xOffset;
    }

    private void sShape_offset(EntropyLayerSettings entropyLayerSettings)
    {
        string posInSubShapeString = ((CommonVars.subShapeLocations)entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.posIndex)).ToString();
        double tmp_xOffset = 0;
        double tmp_yOffset = 0;

        switch (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.subShapeIndex))
        {
            case 0:
                if (posInSubShapeString is "TL" or "TR" or "TS" or "RS" or "LS" or "C")
                {
                    tmp_yOffset = -Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0VerLength));

                    // Half the value for a vertical centering requirement
                    if (posInSubShapeString is "RS" or "LS" or "C")
                    {
                        tmp_yOffset = Convert.ToDouble(tmp_yOffset / 2);
                    }
                }

                if (posInSubShapeString is "TR" or "BR" or "TS" or "RS" or "BS" or "C")
                {
                    tmp_xOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0HorLength));

                    // Half the value for horizontal centering conditions
                    if (posInSubShapeString is "TS" or "BS" or "C")
                    {
                        tmp_xOffset = Convert.ToDouble(tmp_xOffset / 2);
                    }
                }
                break;

            case 1:
                // Subshape 2 is always vertically offset relative to bottom edge of subshape 1 in S.
                if (posInSubShapeString is "TL" or "TR" or "TS" or "RS" or "LS" or "BL" or "BR" or "BS" or "C")
                {
                    tmp_yOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1VerOffset));

                    switch (posInSubShapeString)
                    {
                        // Half the value for a vertical centering requirement
                        case "RS" or "LS" or "C":
                            tmp_yOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1VerLength) / 2);
                            break;
                        // Subtract the value for a subshape 2 bottom edge requirement
                        case "TL" or "TR" or "TS":
                            tmp_yOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1VerLength));
                            break;
                    }
                }

                // Subshape 2 is always pinned to left edge in S. Makes it easy.

                if (posInSubShapeString is "TR" or "BR" or "RS" or "TS" or "BS" or "C")
                {
                    tmp_xOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1HorLength));
                    if (posInSubShapeString is "TS" or "C" or "BS")
                    {
                        tmp_xOffset /= 2;
                    }
                }

                break;

            case 2:
                tmp_yOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0VerLength));
                // Subshape 3 is always offset relative to top edge of subshape 1 in S.
                if (posInSubShapeString is "TL" or "TR" or "TS" or "RS" or "LS" or "BL" or "BR" or "BS" or "C")
                {
                    tmp_yOffset += Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s2VerOffset));

                    switch (posInSubShapeString)
                    {
                        // Half the value for a vertical centering requirement
                        case "RS" or "LS" or "C":
                            tmp_yOffset += Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s2VerLength) / 2);
                            break;
                        // Subtract the value for a subshape 2 bottom edge requirement
                        case "BL" or "BR" or "BS":
                            tmp_yOffset += Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s2VerLength));
                            break;
                    }
                }

                // Subshape 3 is always pinned to right edge in S. Makes it easy.
                tmp_xOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0HorLength));

                switch (posInSubShapeString)
                {
                    case "TL" or "BL" or "LS":
                        tmp_xOffset += Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s2HorLength));
                        break;
                    case "TS" or "BS" or "C":
                        tmp_xOffset += Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s2HorLength) / 2);
                        break;
                }

                break;
        }

        yOffset -= tmp_yOffset;
        xOffset += tmp_xOffset;
    }

    private void doOffsets(EntropyLayerSettings entropyLayerSettings)
    {
        // Use our shape-specific offset calculation methods :
        xOffset = 0;
        yOffset = 0;

        switch (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.shapeIndex))
        {
            case (int)CentralProperties.typeShapes.rectangle:
                rectangle_offset(entropyLayerSettings);
                break;
            case (int)CentralProperties.typeShapes.L:
                lShape_offset(entropyLayerSettings);
                break;
            case (int)CentralProperties.typeShapes.T:
                tShape_offset(entropyLayerSettings);
                break;
            case (int)CentralProperties.typeShapes.X:
                xShape_offset(entropyLayerSettings);
                break;
            case (int)CentralProperties.typeShapes.U:
                uShape_offset(entropyLayerSettings);
                break;
            case (int)CentralProperties.typeShapes.S:
                sShape_offset(entropyLayerSettings);
                break;
            case (int)CentralProperties.typeShapes.BOOLEAN:
            case (int)CentralProperties.typeShapes.GEOCORE:
                // customShape_offset(entropyLayerSettings);
                break;
        }

        // Now for global offset.
        xOffset += Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.gHorOffset));
        yOffset -= Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.gVerOffset));
    }

    public PreviewShape()
    {
        init();
    }

    private void init()
    {
        // Stub to enable direct drive of preview data, primarily for the implant system.
        previewPoints = new List<GeoLibPointF[]>();
        drawnPoly = new List<bool>();
        geoCoreOrthogonalPoly = new List<bool>();
        color = MyColor.Black;
    }

    public PreviewShape(PreviewShape source)
    {
        init(source);
    }

    private void init(PreviewShape source)
    {
        _settingsIndex = source._settingsIndex;
        previewPoints = source.previewPoints.ToList();
        drawnPoly = source.drawnPoly.ToList();
        geoCoreOrthogonalPoly = source.geoCoreOrthogonalPoly.ToList();
        color = new MyColor(source.color);
    }

    public PreviewShape(CommonVars commonVars, int settingsIndex, int subShapeIndex, int mode, bool doPASearch, bool previewMode, int currentRow, int currentCol)
    {
        xOffset = 0;
        yOffset = 0;
        init(commonVars, settingsIndex, subShapeIndex, mode, doPASearch, previewMode, currentRow, currentCol);
    }

    public PreviewShape(CommonVars commonVars, ChaosSettings jobSettings_, int settingsIndex, int subShapeIndex, int mode, bool doPASearch, bool previewMode, int currentRow, int currentCol)
    {
        xOffset = 0;
        yOffset = 0;
        init(commonVars, jobSettings_, settingsIndex, subShapeIndex, mode, doPASearch, previewMode, currentRow, currentCol);
    }

    private bool exitEarly;

    private void distortion(CommonVars commonVars, int settingsIndex)
    {
        for (int poly = 0; poly < previewPoints.Count; poly++)
        {
            switch (drawnPoly[poly])
            {
                // Now let's get some barrel distortion sorted out. Only for non-drawn polygons, and skip if both coefficients are zero to avoid overhead.
                case false when commonVars.getLayerSettings(settingsIndex).getDecimal(EntropyLayerSettings.properties_decimal.lDC1) != 0 || commonVars.getLayerSettings(settingsIndex).getDecimal(EntropyLayerSettings.properties_decimal.lDC2) != 0:
                {
                    int pCount = previewPoints[poly].Length;
#if !VARIANCESINGLETHREADED
                    Parallel.For(0, pCount, point =>
#else
                    for (Int32 point = 0; point < pCount; point++)
#endif
                        {
                            double px = previewPoints[poly][point].X;
                            double py = previewPoints[poly][point].Y;

                            // Need to calculate a new 'radius' from the origin for each point in the polygon, then scale the X/Y values accordingly in the polygon.
                            // Use scale factor to try and guarantee a -1 to +1 value range
                            px /= CentralProperties.scaleFactorForOperation;
                            py /= CentralProperties.scaleFactorForOperation;

                            double oRadius = Math.Sqrt(Utils.myPow(px, 2) + Utils.myPow(py, 2));
                            // Polynomial radial distortion.
                            // rd = r(1 + (k1 * r^2) + (k2 * r^4)) from Zhang, 1999 (https://www.microsoft.com/en-us/research/wp-content/uploads/2016/11/zhan99.pdf)
                            // we only want a scaling factor for our X, Y coordinates.
                            // '1 -' or '1 +' drive the pincushion/barrel tone. Coefficients being negative will have the same effect, so just pick a direction and stick with it.
                            int amplifier = 1000; // scales up end-user values to work within this approach.
                            double t1 = Convert.ToDouble(commonVars.getLayerSettings(settingsIndex).getDecimal(EntropyLayerSettings.properties_decimal.lDC1)) * amplifier * Utils.myPow(Math.Abs(oRadius), 2);
                            double t2 = Convert.ToDouble(commonVars.getLayerSettings(settingsIndex).getDecimal(EntropyLayerSettings.properties_decimal.lDC2)) * Utils.myPow(amplifier, 2) * Utils.myPow(Math.Abs(oRadius), 4);
                            double sFactor = 1 - (t1 + t2);

                            px *= sFactor * CentralProperties.scaleFactorForOperation;
                            py *= sFactor * CentralProperties.scaleFactorForOperation;

                            previewPoints[poly][point] = new GeoLibPointF(px, py);

                        }
#if !VARIANCESINGLETHREADED
                    );
#endif

                    // Re-fragment
                    previewPoints[poly] = fragment.fragmentPath(previewPoints[poly]);
                    break;
                }
            }
        }
    }

    private void doNoise(int noiseType, int seed, double freq, double jitterScale)
    {
        // Gets a -1 to +1 noise field. We get a seed from our RNG of choice unless the layer preview mode is set, where a fixed seed is used.
        // Random constants to mitigate continuity effects in noise that cause nodes in the noise across multiple layers, due to periodicity.
        const double x_const = 123489.1928734;
        const double y_const = 891243.0982134;

        object noiseSource;

        switch (noiseType)
        {
            case (int)CommonVars.noiseIndex.opensimplex:
                noiseSource = new OpenSimplexNoise(seed);
                break;
            case (int)CommonVars.noiseIndex.simplex:
                noiseSource = new SimplexNoise(seed);
                break;
            default:
                noiseSource = new PerlinNoise(seed);
                break;
        }

        // Need to iterate our preview points.
        for (int poly = 0; poly < previewPoints.Count; poly++)
        {
            if (previewPoints[poly].Length <= 1 || drawnPoly[poly])
            {
                continue;
            }
            GeoLibPointF[] mcPoints = previewPoints[poly].ToArray();
            int ptCount = mcPoints.Length;

            // Create our jittered polygon in a new list to avoid breaking normal computation, etc. by modifying the source.
            GeoLibPointF[] jitteredPoints = new GeoLibPointF[ptCount];

            // We could probably simply cast rays in the raycaster and use those, but for now reinvent the wheel here...
            GeoLibPointF[] normals = new GeoLibPointF[ptCount];
            GeoLibPointF[] previousNormals = new GeoLibPointF[ptCount];
            // Pre-calculate these for the threading to be an option.
            // This is a serial evaluation as we need both the previous and the current normal for each point.
#if !VARIANCESINGLETHREADED
            Parallel.For(0, ptCount - 1, pt => 
#else
                for (Int32 pt = 0; pt < ptCount - 1; pt++)
#endif
                {
                    double dx;
                    double dy;
                    if (pt == 0)
                    {
                        // First vertex needs special care.
                        dx = mcPoints[0].X - mcPoints[ptCount - 2].X;
                        dy = mcPoints[0].Y - mcPoints[ptCount - 2].Y;
                    }
                    else
                    {
                        dx = mcPoints[pt + 1].X - mcPoints[pt].X;
                        dy = mcPoints[pt + 1].Y - mcPoints[pt].Y;
                    }
                    normals[pt] = new GeoLibPointF(-dy, dx);
                }
#if !VARIANCESINGLETHREADED
            );
#endif
            normals[^1] = new GeoLibPointF(normals[0]);

            int nLength = normals.Length;
#if !VARIANCESINGLETHREADED
            Parallel.For(1, nLength, pt => 
#else
                for (int pt = 1; pt < nLength; pt++)
#endif
                {
                    previousNormals[pt] = new GeoLibPointF(normals[pt - 1]);
                }
#if !VARIANCESINGLETHREADED
            );
#endif

            previousNormals[0] = new GeoLibPointF(normals[^2]);

#if !VARIANCESINGLETHREADED
            Parallel.For(0, ptCount - 1, pt =>
#else
                for (int pt = 0; pt < ptCount - 1; pt++)
#endif
                {
                    // We need to average the normals of two edge segments to get the vector we need to displace our point along.
                    // This ensures that we handle corners and fluctuations in a reasonable manner.
                    GeoLibPointF averagedEdgeNormal = new((previousNormals[pt].X + normals[pt].X) / 2.0f, (previousNormals[pt].Y + normals[pt].Y) / 2.0f);
                    // Normalize our vector length.
                    double length = Math.Sqrt(Utils.myPow(averagedEdgeNormal.X, 2) + Utils.myPow(averagedEdgeNormal.Y, 2));
                    const double normalTolerance = 1E-3;
                    if (length < normalTolerance)
                    {
                        length = normalTolerance;
                    }
                    averagedEdgeNormal.X /= length;
                    averagedEdgeNormal.Y /= length;

                    // Use a tolerance as we're handling floats; we don't expect a normalized absolute value generally above 1.0, ignoring the float error.
                    /*
                    if ((Math.Abs(averagedEdgeNormal.X) - 1 > normalTolerance) || (Math.Abs(averagedEdgeNormal.Y) - 1 > normalTolerance))
                    {
                        ErrorReporter.showMessage_OK("averageNormal exceeded limits: X:" + averagedEdgeNormal.X.ToString() + ",Y:" + averagedEdgeNormal.Y.ToString(), "oops");
                    }
                    */

                    // We can now modify the position of our point and stuff it into our jittered list.
                    double jitterAmount;

                    switch (noiseType)
                    {
                        case (int)CommonVars.noiseIndex.opensimplex:
                            jitterAmount = ((OpenSimplexNoise)noiseSource).Evaluate(freq * (mcPoints[pt].X + x_const), freq * (mcPoints[pt].Y + y_const));
                            break;
                        case (int)CommonVars.noiseIndex.simplex:
                            jitterAmount = ((SimplexNoise)noiseSource).GetNoise(freq * (mcPoints[pt].X + x_const), freq * (mcPoints[pt].Y + y_const));
                            break;
                        default:
                            jitterAmount = ((PerlinNoise)noiseSource).Noise(freq * (mcPoints[pt].X + x_const), freq * (mcPoints[pt].Y + y_const), 0);
                            break;
                    }

                    jitterAmount *= jitterScale;

                    double jitteredX = mcPoints[pt].X;
                    jitteredX += jitterAmount * averagedEdgeNormal.X;

                    double jitteredY = mcPoints[pt].Y;
                    jitteredY += jitterAmount * averagedEdgeNormal.Y;

                    jitteredPoints[pt] = new GeoLibPointF(jitteredX, jitteredY);
                }
#if !VARIANCESINGLETHREADED
            );
#endif
            jitteredPoints[ptCount - 1] = new GeoLibPointF(jitteredPoints[0]);

            // Push back to mcPoints for further processing.
            previewPoints[poly] = jitteredPoints.ToArray();
        }
    }

    private void applyNoise(bool previewMode, CommonVars commonVars, ChaosSettings jobSettings, int settingsIndex)
    {

        EntropyLayerSettings entropyLayerSettings = commonVars.getLayerSettings(settingsIndex);

        double lwrConversionFactor;
        lwrConversionFactor = commonVars.getSimulationSettings().getValue(EntropySettings.properties_i.ler) == 1 ? Math.Sqrt(2) : 0.5f;

        // LWR, skip if not requested to avoid runtime pain
        if ((!previewMode || entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.lwrPreview) == 1) && entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.lwr) != 0)
        {
            double jitterScale = Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.lwr)) / lwrConversionFactor; // LWR jitter of edge; use RSS for stricter assessment
            if (!previewMode && entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.lwrPreview) == 1 && !jobSettings.getPreviewMode())
            {
                // This used to be easier, but now we have the case of a non-preview mode, but the layer setting calls for a preview.
                jitterScale *= jobSettings.getValue(ChaosSettings.properties.LWRVar, settingsIndex);
            }

            doNoise(
                noiseType: entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.lwrType),
                seed: jobSettings.getInt(ChaosSettings.ints.lwrSeed, settingsIndex),
                freq: Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.lwrFreq)),
                jitterScale: jitterScale
            );
        }

        switch (previewMode)
        {
            // LWR2, skip if not requested to avoid runtime pain
            case false or true when entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.lwr2) != 0:
            {
                double jitterScale = Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.lwr2)) / lwrConversionFactor; // LWR jitter of edge; use RSS for stricter assessment
                if (!previewMode && entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.lwrPreview) == 1 && !jobSettings.getPreviewMode())
                {
                    // This used to be easier, but now we have the case of a non-preview mode, but the layer setting calls for a preview.
                    jitterScale *= jobSettings.getValue(ChaosSettings.properties.LWR2Var, settingsIndex);
                }

                doNoise(
                    noiseType: entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.lwr2Type),
                    seed: jobSettings.getInt(ChaosSettings.ints.lwr2Seed, settingsIndex),
                    freq: Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.lwr2Freq)),
                    jitterScale: jitterScale
                );
                break;
            }
        }
    }

    private void proximityBias(CommonVars commonVars, int settingsIndex)
    {
        // Proximity biasing - where isolated edges get bias based on distance to nearest supporting edge.

        EntropyLayerSettings entropyLayerSettings = commonVars.getLayerSettings(settingsIndex);

        bool proxBiasNeeded = entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.pBias) != 0 && entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.pBiasDist) != 0;

        if (!proxBiasNeeded)
        {
            return;
        }

        bool debug = false;
        bool linear = false;

        List<GeoLibPointF[]> preOverlapMergePolys = new();

        Paths dRays = new();

        // Scale up our geometry for processing. Force a clockwise point order here due to potential upstream point order changes (e.g. polygon merging)
        Paths sourceGeometry = GeoWrangler.pathsFromPointFs(previewPoints, CentralProperties.scaleFactorForOperation);

        int sCount = sourceGeometry.Count;
        for (int poly = 0; poly < sCount; poly++)
        {
            if (sourceGeometry[poly].Count <= 1 || drawnPoly[poly])
            {
                // Nothing to do with drawn or zero count entries.
                continue;
            }

            Path sourcePoly = sourceGeometry[poly].ToList();
            Paths collisionGeometry = sourceGeometry.ToList();
            // collisionGeometry.RemoveAt(poly); // Don't actually want to remove the emission as self-aware proximity matters.
            Path deformedPoly = new();

            // Threading operation here gets more tricky than the distance handler. We have a less clear trade off of threading based on the emission edge (the polygon being biased) vs the multisampling emission.
            // In batch calculation mode, this tradeoff gets more awkward.
            // Threading both options also causes major performance degradation as far too many threads are spawned for the host system.
            bool multiSampleThread = false;
            bool emitThread = false;

            if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.proxRays) > 1)
            {
                multiSampleThread = true;
                // for multipolygon scenarios, avoid threading the multisampling and instead favor threading emitting edge.
                if (sourceGeometry.Count > 1)
                {
                    emitThread = true;
                    multiSampleThread = false;
                }
            }
            else
            {
                emitThread = true;
            }

            Fragmenter f = new(commonVars.getSimulationSettings().getResolution() * CentralProperties.scaleFactorForOperation);

            sourcePoly = f.fragmentPath(sourcePoly);

            collisionGeometry = f.fragmentPaths(collisionGeometry);

            RayCast rc = new(sourcePoly, collisionGeometry, Convert.ToInt32(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.pBiasDist) * CentralProperties.scaleFactorForOperation), false, invert:false, entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.proxRays), emitThread, multiSampleThread, sideRayFallOff: (RayCast.falloff)entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.proxSideRaysFallOff), sideRayFallOffMultiplier: Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.proxSideRaysMultiplier)));

            Paths clippedLines = rc.getClippedRays().ToList();
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (debug)
            {
                dRays.AddRange(clippedLines);
            }

            // We hope to get the same number of clipped lines back as the number of points that went in....
            int cLCount = clippedLines.Count;
            for (int line = 0; line < cLCount; line++)
            {
                long displacedX = sourcePoly[line].X;
                long displacedY = sourcePoly[line].Y;

                double lineLength = rc.getRayLength(line);

                switch (lineLength)
                {
                    // No biasing - ray never made it beyond the surface. Short-cut the 
                    case 0:
                        deformedPoly.Add(new IntPoint(clippedLines[line][0]));
                        continue;
                    case < 0:
                        lineLength *= -1;
                        break;
                }

                // Calculate our bias based on this distance and apply it.
                double biasScaling = lineLength / CentralProperties.scaleFactorForOperation / Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.pBiasDist));

                if (biasScaling > 1)
                {
                    biasScaling = 1;
                }

                // Probably should be a sigmoid, but using this for now.
                double displacedAmount;

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (linear)
                {
                    displacedAmount = biasScaling * Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.pBias)) * CentralProperties.scaleFactorForOperation;
                }
                else
                {
                    // Using sine to make a ease-in/ease-out effect.
                    displacedAmount = Math.Sin(Utils.toRadians(biasScaling * 90.0f)) * Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.pBias)) * CentralProperties.scaleFactorForOperation;
                }

                // Use our cast ray from rc to get a normalized average 
                IntPoint averagedEdgeNormal = GeoWrangler.intPoint_distanceBetweenPoints(clippedLines[line][clippedLines[line].Count - 1], clippedLines[line][0]);

                // Normalize our vector length.
                double aX = averagedEdgeNormal.X / lineLength;
                double aY = averagedEdgeNormal.Y / lineLength;

                displacedY += (long)(displacedAmount * aY);
                displacedX += (long)(displacedAmount * aX);

                deformedPoly.Add(new IntPoint(displacedX, displacedY));
            }
            preOverlapMergePolys.Add(GeoWrangler.pointFFromPath(deformedPoly, CentralProperties.scaleFactorForOperation));
            deformedPoly.Add(new IntPoint(deformedPoly[0]));
        }

        // Check for overlaps and process as needed post-biasing.
        processOverlaps(commonVars, settingsIndex, preOverlapMergePolys, forceOverride: false);

        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        if (!debug)
        {
            return;
        }

        foreach (Path t in dRays)
        {
            previewPoints.Add(GeoWrangler.pointFFromPath(t, CentralProperties.scaleFactorForOperation));
            drawnPoly.Add(true);
        }
    }

    private void init(CommonVars commonVars, int settingsIndex, int subShapeIndex, int mode, bool doPASearch, bool previewMode, int currentRow, int currentCol)
    {
        ChaosSettings jobSettings_ = new(previewMode, commonVars.getListOfSettings(), commonVars.getSimulationSettings());
        init(commonVars, jobSettings_, settingsIndex, subShapeIndex, mode, doPASearch, previewMode, currentRow, currentCol);
    }

    private void init(CommonVars commonVars, ChaosSettings chaosSettings, int settingsIndex, int subShapeIndex, int mode, bool doPASearch, bool previewMode, int currentRow, int currentCol, EntropyLayerSettings entropyLayerSettings = null, bool doClockwiseGeoFix = true, bool process_overlaps = true)
    {
        _settingsIndex = settingsIndex;
        try
        {
            DOEDependency = false;
            fragment = new Fragmenter(commonVars.getSimulationSettings().getResolution(), CentralProperties.scaleFactorForOperation);
            previewPoints = new List<GeoLibPointF[]>();
            drawnPoly = new List<bool>();
            geoCoreOrthogonalPoly = new List<bool>();
            color = MyColor.Black; // overridden later.

            switch (entropyLayerSettings)
            {
                case null:
                    entropyLayerSettings = commonVars.getLayerSettings(settingsIndex);
                    break;
            }
            if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.shapeIndex) == (int)CommonVars.shapeNames.GEOCORE)
            {
                init_geoCore(commonVars, chaosSettings, settingsIndex, entropyLayerSettings, mode, doPASearch, previewMode, process_overlaps, doClockwiseGeoFix);
                // Get our offsets configured. We need to check for DOE settings here, to prevent relocation of extracted polygons within the tile during offset evaluation.
                if (commonVars.getSimulationSettings().getDOESettings().getLayerAffected(settingsIndex) == 1)
                {
                    DOEDependency = true;
                    commonVars.getSimulationSettings().getDOESettings().getDouble(DOESettings.properties_d.colOffset);
                    commonVars.getSimulationSettings().getDOESettings().getDouble(DOESettings.properties_d.rowOffset);
                }
                doOffsets(entropyLayerSettings);
            }
            else // not geoCore related.
            {
                if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.shapeIndex) == (int)CommonVars.shapeNames.BOOLEAN)
                {
                    try
                    {
                        init_boolean(commonVars, chaosSettings, settingsIndex, subShapeIndex, mode, doPASearch, previewMode, currentRow, currentCol, entropyLayerSettings);
                        // Get our offsets configured.
                        // Is any input layer coming from a GDS DOE tile? We need to check for DOE settings here, to prevent relocation of extracted polygons within the tile during offset evaluation.
                        int boolLayer = entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.bLayerA);
                        while (boolLayer > 0)
                        {
                            DOEDependency = commonVars.getSimulationSettings().getDOESettings().getLayerAffected(boolLayer) == 1;
                            if (DOEDependency)
                            {
                                break;
                            }
                            boolLayer = commonVars.getLayerSettings(boolLayer).getInt(EntropyLayerSettings.properties_i.bLayerA);
                        }
                        if (!DOEDependency)
                        {
                            boolLayer = entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.bLayerB);
                            while (boolLayer > 0)
                            {
                                DOEDependency = commonVars.getSimulationSettings().getDOESettings().getLayerAffected(boolLayer) == 1;
                                if (DOEDependency)
                                {
                                    break;
                                }
                                boolLayer = commonVars.getLayerSettings(boolLayer).getInt(EntropyLayerSettings.properties_i.bLayerB);
                            }
                        }
                        if (DOEDependency)
                        {
                            commonVars.getSimulationSettings().getDOESettings().getDouble(DOESettings.properties_d.colOffset);
                            commonVars.getSimulationSettings().getDOESettings().getDouble(DOESettings.properties_d.rowOffset);
                        }

                        doOffsets(entropyLayerSettings);
                    }
                    catch (Exception)
                    {
                    }
                    exitEarly = true; // avoid second pass of distortion, etc.
                }
                else
                {
                    if (mode == 0)
                    {
                        // Basic shape - 5 points to make a closed preview. 5th is identical to 1st.
                        GeoLibPointF[] tempArray = new GeoLibPointF[5];

                        // Need exception handling here for overflow cases?
                        decimal bottom_leftX = 0, bottom_leftY = 0;
                        decimal top_leftX = 0, top_leftY = 0;
                        decimal top_rightX = 0, top_rightY = 0;
                        decimal bottom_rightX = 0, bottom_rightY = 0;
                        switch (subShapeIndex)
                        {
                            case 0:
                                bottom_leftX = 0;
                                bottom_leftY = 0;
                                top_leftX = 0;
                                top_leftY = entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0VerLength);
                                top_rightX = entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0HorLength);
                                top_rightY = entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0VerLength);
                                bottom_rightX = entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0HorLength);
                                bottom_rightY = 0;
                                xOffset = Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0HorOffset));
                                yOffset = Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0VerOffset));
                                break;
                            case 1:
                                bottom_leftX = 0;
                                bottom_leftY = 0;
                                top_leftX = 0;
                                top_leftY = entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1VerLength);
                                top_rightX = entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1HorLength);
                                top_rightY = entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1VerLength);
                                bottom_rightX = entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1HorLength);
                                bottom_rightY = 0;
                                xOffset = Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0HorOffset) + entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1HorOffset));
                                yOffset = Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0VerOffset) + entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s1VerOffset));
                                break;
                            case 2:
                            {
                                bottom_leftX = 0;
                                bottom_leftY = 0;
                                top_leftX = 0;
                                top_leftY = entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s2VerLength);
                                top_rightX = entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s2HorLength);
                                top_rightY = entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s2VerLength);
                                bottom_rightX = entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s2HorLength);
                                bottom_rightY = 0;
                                xOffset = Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s2HorOffset) + entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0HorOffset));
                                yOffset = -Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s2VerOffset) + entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s2VerLength) + entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0VerOffset));
                                if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.shapeIndex) == (int)CommonVars.shapeNames.Sshape)
                                {
                                    yOffset += Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0VerLength)); // offset our subshape to put it in the correct place in the UI.
                                }

                                break;
                            }
                        }

                        // Populate array.
                        tempArray[0] = new GeoLibPointF((double)bottom_leftX, (double)bottom_leftY);
                        tempArray[1] = new GeoLibPointF((double)top_leftX, (double)top_leftY);
                        tempArray[2] = new GeoLibPointF((double)top_rightX, (double)top_rightY);
                        tempArray[3] = new GeoLibPointF((double)bottom_rightX, (double)bottom_rightY);
                        tempArray[4] = new GeoLibPointF(tempArray[0]);

                        // Apply our deltas
                        int tLength = tempArray.Length;
#if !VARIANCESINGLETHREADED
                        Parallel.For(0, tLength, i => 
#else
                            for (Int32 i = 0; i < tLength; i++)
#endif
                            {
                                tempArray[i].X += xOffset;
                                tempArray[i].Y += yOffset;
                            }
#if !VARIANCESINGLETHREADED
                        );
#endif
                        previewPoints.Add(tempArray);
                        drawnPoly.Add(true);
                    }
                    else
                    {
                        // Complex shape
                        try
                        {
                            EntropyShape complexPoints = new(commonVars.getSimulationSettings(), commonVars.getListOfSettings(), settingsIndex, doPASearch, previewMode, chaosSettings);
                            previewPoints.Add(complexPoints.getPoints());
                            drawnPoly.Add(false);
                        }
                        catch (Exception)
                        {

                        }
                    }
                    // Get our offsets configured.
                    doOffsets(entropyLayerSettings);

                    int pCount = previewPoints.Count;
                    for (int poly = 0; poly < pCount; poly++)
                    {
                        int ptCount = previewPoints[poly].Length;
#if !VARIANCESINGLETHREADED
                        Parallel.For(0, ptCount, point =>
#else
                            for (Int32 point = 0; point < ptCount; point++)
#endif
                            {
                                double px = previewPoints[poly][point].X + xOffset;
                                double py = previewPoints[poly][point].Y - yOffset;

                                previewPoints[poly][point] = new GeoLibPointF(px, py);
                            }
#if !VARIANCESINGLETHREADED
                        );
#endif
                        if (Math.Abs(previewPoints[poly][0].X - previewPoints[poly][previewPoints[poly].Length - 1].X) > double.Epsilon ||
                            Math.Abs(previewPoints[poly][0].Y - previewPoints[poly][previewPoints[poly].Length - 1].Y) > double.Epsilon)
                        {
                            ErrorReporter.showMessage_OK("Start and end not the same - previewShape", "Oops");
                        }
                    }
                }
            }

            if (exitEarly || mode != 1)
            {
                return;
            }

            // Apply lens distortion.
            distortion(commonVars, settingsIndex);
            // Noise and proximity biasing.
            applyNoise(previewMode, commonVars, chaosSettings, settingsIndex);
            proximityBias(commonVars, settingsIndex);
        }
        catch (Exception)
        {
        }
    }

    private void init_geoCore(CommonVars commonVars, ChaosSettings chaosSettings, int settingsIndex, EntropyLayerSettings entropyLayerSettings, int mode, bool doPASearch, bool previewMode, bool process_overlaps, bool forceClockwise)
    {
        // We'll use these to shift the points around.
        double xOverlayVal = 0.0f;
        double yOverlayVal = 0.0f;

        xOffset = Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.gHorOffset) + entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0HorOffset));
        yOffset = Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.gVerOffset) + entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0VerOffset));

        // OK. We need to crop our layout based on the active tile if there is a DOE flag set.
        bool tileHandlingNeeded = commonVars.getSimulationSettings().getDOESettings().getLayerAffected(settingsIndex) == 1;

        if (mode == 1)
        {
            // We need this check and early return because previewShape is now used in the layer preview
            // mode to handle bias on geoCore elements. Populating this when the layer is not enabled
            // causes the shared structure with the simulation engine to be defined and breaks everything.
            // Instead we just make a zero area polygon (to avoid issues downstream) and return early.
            if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.enabled) == 0)
            {
                previewPoints.Add(new GeoLibPointF[4]);
                for (int i = 0; i < 4; i++)
                {
                    previewPoints[0][i] = new GeoLibPointF(0, 0);
                }
                drawnPoly.Add(false);
                geoCoreOrthogonalPoly.Add(true);
                return;
            }

            switch (previewMode)
            {
                case true when tileHandlingNeeded:
                {
                    if (!commonVars.getLayerPreviewDOETile())
                    {
                        tileHandlingNeeded = false;
                    }

                    break;
                }
                // Get overlay figured out.
                case false:
                {
                    xOverlayVal = chaosSettings.getValue(ChaosSettings.properties.overlayX, settingsIndex) * Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.xOL));
                    yOverlayVal = chaosSettings.getValue(ChaosSettings.properties.overlayY, settingsIndex) * Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.yOL));

                    // Handle overlay reference setting
                    if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.xOL_av) == 1) // overlay average
                    {
                        List<double> overlayValues = new();
                        for (int avgolref_x = 0; avgolref_x < entropyLayerSettings.getIntArray(EntropyLayerSettings.properties_intarray.xOLRefs).Length; avgolref_x++)
                        {
                            if (entropyLayerSettings.getIntArrayValue(EntropyLayerSettings.properties_intarray.xOLRefs, avgolref_x) == 1)
                            {
                                overlayValues.Add(chaosSettings.getValue(ChaosSettings.properties.overlayX, avgolref_x) * Convert.ToDouble(commonVars.getLayerSettings(avgolref_x).getDecimal(EntropyLayerSettings.properties_decimal.xOL))); // Overlay shift
                            }
                        }

                        xOverlayVal += overlayValues.Average();
                    }
                    else // vanilla overlay reference mode
                    {
                        if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.xOL_ref) != -1)
                        {
                            xOverlayVal += chaosSettings.getValue(ChaosSettings.properties.overlayX, entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.xOL_ref)) * Convert.ToDouble(commonVars.getLayerSettings(entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.xOL_ref)).getDecimal(EntropyLayerSettings.properties_decimal.xOL));
                        }
                    }

                    if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.yOL_av) == 1) // overlay average
                    {
                        List<double> overlayValues = new();
                        for (int avgolref_y = 0; avgolref_y < entropyLayerSettings.getIntArray(EntropyLayerSettings.properties_intarray.yOLRefs).Length; avgolref_y++)
                        {
                            if (entropyLayerSettings.getIntArrayValue(EntropyLayerSettings.properties_intarray.yOLRefs, avgolref_y) == 1)
                            {
                                overlayValues.Add(chaosSettings.getValue(ChaosSettings.properties.overlayY, avgolref_y) * Convert.ToDouble(commonVars.getLayerSettings(avgolref_y).getDecimal(EntropyLayerSettings.properties_decimal.yOL))); // Overlay shift
                            }
                        }

                        yOverlayVal += overlayValues.Average();
                    }
                    else // vanilla overlay reference mode
                    {
                        if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.yOL_ref) != -1)
                        {
                            yOverlayVal += chaosSettings.getValue(ChaosSettings.properties.overlayY, entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.yOL_ref)) * Convert.ToDouble(commonVars.getLayerSettings(entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.yOL_ref)).getDecimal(EntropyLayerSettings.properties_decimal.yOL));
                        }
                    }

                    break;
                }
            }

            // Decouple the geometry here to avoid manipulation going back to original source.
            List<GeoLibPointF[]> tempPolyList;
            switch (tileHandlingNeeded)
            {
                case true:
                    tempPolyList = commonVars.getNonSimulationSettings().extractedTile[settingsIndex].ToList();
                    break;
                default:
                    tempPolyList = entropyLayerSettings.getFileData().ToList();
                    break;
            }
            try
            {
                double minx = tempPolyList[0][0].X;
                double miny = tempPolyList[0][0].Y;
                double maxx = tempPolyList[0][0].X;
                double maxy = tempPolyList[0][0].Y;
                int tPCount = tempPolyList.Count;
                for (int poly = 0; poly < tPCount; poly++)
                {
                    double min_x = tempPolyList[poly].Min(p => p.X);
                    double min_y = tempPolyList[poly].Min(p => p.Y);
                    double max_x = tempPolyList[poly].Max(p => p.X);
                    double max_y = tempPolyList[poly].Max(p => p.Y);

                    if (min_x < minx)
                    {
                        minx = min_x;
                    }
                    if (min_y < miny)
                    {
                        miny = min_y;
                    }
                    if (max_x > maxx)
                    {
                        maxx = max_x;
                    }
                    if (max_y > maxy)
                    {
                        maxy = max_y;
                    }
                }

                GeoLibPointF bb_mid = new(minx + (maxx - minx) / 2.0f, miny + (maxy - miny) / 2.0f);
                bb_mid.X += xOverlayVal + (double)entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.gHorOffset) + (double)entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0HorOffset);
                bb_mid.Y += yOverlayVal + (double)entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.gVerOffset) + (double)entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.s0VerOffset);

                if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.perPoly) == 1)
                {
                    bb_mid = null;
                }

                for (int poly = 0; poly < tPCount; poly++)
                {
                    GeoLibPointF[] tempPoly;

                    if (tileHandlingNeeded)
                    {
                        // Poly is already closed - presents a problem if we use contouring.
                        int arraySize = tempPolyList[poly].Length;

                        if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.gCSEngine) == 1)
                        {
                            if (Math.Abs(tempPolyList[poly][0].X - tempPolyList[poly][tempPolyList[poly].Length - 1].X) < double.Epsilon && Math.Abs(tempPolyList[poly][0].Y - tempPolyList[poly][tempPolyList[poly].Length - 1].Y) < double.Epsilon)
                            {
                                arraySize--;
                            }
                        }

                        tempPoly = new GeoLibPointF[arraySize];

#if !VARIANCESINGLETHREADED
                        Parallel.For(0, arraySize, pt => 
#else
                            for (int pt = 0; pt < arraySize; pt++)
#endif
                            {
                                tempPoly[pt] = new GeoLibPointF(tempPolyList[poly][pt].X + xOffset, tempPolyList[poly][pt].Y + yOffset);
                            }
#if !VARIANCESINGLETHREADED
                        );
#endif
                    }
                    else
                    {
                        int polySize = entropyLayerSettings.getFileData()[poly].Length;

                        tempPoly = new GeoLibPointF[polySize];

#if !VARIANCESINGLETHREADED
                        Parallel.For(0, polySize, pt => 
#else
                            for (Int32 pt = 0; pt < polySize; pt++)
#endif
                            {
                                tempPoly[pt] = new GeoLibPointF(entropyLayerSettings.getFileData()[poly][pt].X + xOffset, entropyLayerSettings.getFileData()[poly][pt].Y + yOffset);
                            }
#if !VARIANCESINGLETHREADED
                        );
#endif
                    }

                    bool drawn = false;

                    // Compatibility shim - we need to toggle this behavior due to the ILB passing in mixed orientation geometry that we don't want to clobber.
                    // However, external geometry may need this spin fixing. Although the upper levels should also re-spin geometry properly - we don't assume this.
                    if (forceClockwise)
                    {
                        tempPoly = GeoWrangler.clockwiseAndReorder(tempPoly); // force clockwise order and lower-left at 0 index.
                    }

                    // Strip termination points. Set shape will take care of additional clean-up if needed.
                    tempPoly = GeoWrangler.stripTerminators(tempPoly, false);

                    if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.gCSEngine) == 0)
                    {
                        previewPoints.Add(fragment.fragmentPath(tempPoly.ToArray()));
                        geoCoreOrthogonalPoly.Add(false); // We need to populate the list, but in this non-contoured case, the value doesn't matter.
                    }
                    else
                    {
                        // Feed tempPoly to shape engine.
                        ShapeLibrary shape = new(entropyLayerSettings);

                        shape.setShape(entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.shapeIndex), tempPoly.ToArray()); // feed the shape engine with the geometry using our optional parameter.
                        EntropyShape complexPoints = new(commonVars.getSimulationSettings(), commonVars.getListOfSettings(), settingsIndex, doPASearch, previewMode, chaosSettings, shape, bb_mid);
                        // Add resulting shape to the previewPoints.
                        previewPoints.Add(complexPoints.getPoints());
                        // This list entry does matter - we need to choose the right expansion method in case contouring has been chosen, but the
                        // polygon is not orthogonal.
                        geoCoreOrthogonalPoly.Add(shape.geoCoreShapeOrthogonal);
                    }
                    drawnPoly.Add(drawn);
                }
            }
            catch (Exception)
            {
            }

            // Overlay
            if (!previewMode)
            {
                int pCount = previewPoints.Count;
                for (int poly = 0; poly < pCount; poly++)
                {
                    if (drawnPoly[poly])
                    {
                        continue;
                    }

                    int ptCount = previewPoints[poly].Length;
#if !VARIANCESINGLETHREADED
                    Parallel.For(0, ptCount, pt =>
#else
                            for (int pt = 0; pt < ptCount; pt++)
#endif
                        {
                            previewPoints[poly][pt].X += xOverlayVal;
                            previewPoints[poly][pt].Y += yOverlayVal;
                        }
#if !VARIANCESINGLETHREADED
                    );
#endif
                }
            }

            // Biasing and CDU thanks to clipperLib
            // Note that we have to guard against a number of situations here
            // We do not want to re-bias contoured geoCore data - it's been done already.
            // Additionally, we don't want to assume an overlap for processing where none exists : we'll get back an empty polygon.
            double globalBias_Sides = Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.sBias));
            globalBias_Sides += chaosSettings.getValue(ChaosSettings.properties.CDUSVar, settingsIndex) * Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.sCDU)) / 2;
            List<GeoLibPointF[]> resizedLayoutData = new();
            try
            {
                if (globalBias_Sides > double.Epsilon)
                {
                    List<bool> new_Drawn = new();

                    int pCount = previewPoints.Count;
                    for (int poly = 0; poly < pCount; poly++)
                    {
                        // Need to iterate across all polygons and only bias in this manner either:
                        // non-contoured mode
                        // contoured, but non-orthogonal polygons.
                        if (entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.gCSEngine) == 0 ||
                            !geoCoreOrthogonalPoly[poly] && entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.gCSEngine) == 1)
                        {
                            Paths resizedPolyData = new();
                            Path gdsPointData = GeoWrangler.pathFromPointF(previewPoints[poly], CentralProperties.scaleFactorForOperation);
                            ClipperOffset co = new();
                            co.AddPath(gdsPointData, JoinType.jtMiter, EndType.etClosedPolygon);
                            co.Execute(ref resizedPolyData, Convert.ToDouble(globalBias_Sides * CentralProperties.scaleFactorForOperation));

                            // Store our polygon data (note that we could have ended up with two or more polygons due to reduction)
                            try
                            {
                                foreach (GeoLibPointF[] rPolyData in resizedPolyData.Select(t => GeoWrangler.pointFFromPath(t, CentralProperties.scaleFactorForOperation)))
                                {
                                    resizedLayoutData.Add(rPolyData);

                                    // We need to track our drawn state as we could have a polygon count change.
                                    new_Drawn.Add(drawnPoly[poly]);
                                }
                            }
                            catch (Exception)
                            {
                            }
                        }
                        else
                        {
                            new_Drawn.Add(drawnPoly[poly]);
                        }

                        // In case of contoured mode, with orthogonal polygon, we need to store this:
                        if (geoCoreOrthogonalPoly[poly] && entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.gCSEngine) == 1)
                        {
                            // Decouple out of paranoia.
                            resizedLayoutData.Add(previewPoints[poly].ToArray());
                        }
                    }

                    previewPoints = resizedLayoutData.ToList();
                    drawnPoly = new_Drawn.ToList();
                }
            }
            catch (Exception)
            {

            }

            if (process_overlaps)
            {
                processOverlaps(commonVars, settingsIndex, previewPoints, forceOverride: false, (PolyFillType)commonVars.getLayerSettings(settingsIndex).getInt(EntropyLayerSettings.properties_i.fill));
            }
        }
        else
        {
            // Drawn polygons only.
            // Needed to take this approach, otherwise fileData gets tied to the previewPoints list and things go wrong quickly.
            // .ToList() was insufficient to avoid the link.
            
            // Decouple the geometry here to avoid manipulation going back to original source.
            switch (tileHandlingNeeded)
            {
                case true:
                    List<GeoLibPointF[]> tempPolyList = commonVars.getNonSimulationSettings().extractedTile[settingsIndex].ToList();
                    foreach (GeoLibPointF[] t in tempPolyList)
                    {
                        previewPoints.Add(GeoWrangler.close(t));
                        drawnPoly.Add(true);
                    }
                    break;
                default:
                    for (int poly = 0; poly < entropyLayerSettings.getFileData().Count; poly++)
                    {
                        int arraySize = entropyLayerSettings.getFileData()[poly].Length;
                        GeoLibPointF[] tmp = new GeoLibPointF[arraySize];
#if !VARIANCESINGLETHREADED
                        Parallel.For(0, arraySize, pt => 
#else
                    for (Int32 pt = 0; pt < arraySize; pt++)
#endif
                            {
                                tmp[pt] = new GeoLibPointF(entropyLayerSettings.getFileData()[poly][pt].X + xOffset,
                                    entropyLayerSettings.getFileData()[poly][pt].Y + yOffset);
                            }
#if !VARIANCESINGLETHREADED
                        );
#endif
                        previewPoints.Add(tmp);
                        drawnPoly.Add(true);
                    }
                    break;
            }

        }
    }

    private void init_boolean(CommonVars commonVars, ChaosSettings chaosSettings, int settingsIndex, int subShapeIndex, int mode, bool doPASearch, bool previewMode, int currentRow, int currentCol, EntropyLayerSettings entropyLayerSettings)
    {
        // Get our two layers' geometry. Avoid keyholes in the process.
        int layerAIndex = entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.bLayerA);
        if (settingsIndex == layerAIndex || layerAIndex < 0)
        {
            return;
        }
        EntropyLayerSettings layerA = commonVars.getLayerSettings(layerAIndex);
        PreviewShape a_pShape = new(commonVars, layerAIndex, layerA.getInt(EntropyLayerSettings.properties_i.subShapeIndex), mode: 1, doPASearch, previewMode, currentRow, currentCol);

        int layerBIndex = entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.bLayerB);
        if (settingsIndex == layerBIndex || layerBIndex < 0)
        {
            return;
        }
        EntropyLayerSettings layerB = commonVars.getLayerSettings(layerBIndex);
        PreviewShape b_pShape = new(commonVars, layerBIndex, layerB.getInt(EntropyLayerSettings.properties_i.subShapeIndex), mode: 1, doPASearch, previewMode, currentRow, currentCol);

        // We need to map the geometry into Paths for use in the Boolean
        Paths layerAPaths = GeoWrangler.pathsFromPointFs(a_pShape.getPoints(), CentralProperties.scaleFactorForOperation);
        Paths layerBPaths = GeoWrangler.pathsFromPointFs(b_pShape.getPoints(), CentralProperties.scaleFactorForOperation);

        // Now this gets interesting. We leverage the Boolean engine in ChaosEngine to get the result we want.
        // This should probably be relocated at some point, but for now, it's an odd interaction.
        Paths booleanPaths = ChaosEngine.customBoolean(
            firstLayerOperator: entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.bLayerOpA),
            firstLayer: layerAPaths, 
            secondLayerOperator: entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.bLayerOpB), 
            secondLayer: layerBPaths, 
            booleanFlag: entropyLayerSettings.getInt(EntropyLayerSettings.properties_i.bLayerOpAB),
            resolution: commonVars.getSimulationSettings().getResolution(),
            extension: Convert.ToDouble(entropyLayerSettings.getDecimal(EntropyLayerSettings.properties_decimal.rayExtension))
        );

        // This is set later, if needed, to force an early return from the overlap processing path.
        int bpCount = booleanPaths.Count;
#if !VARIANCESINGLETHREADED
        Parallel.For(0, bpCount, i => 
#else
            for (int i = 0; i < bpCount; i++)
#endif
            {
                try
                {
                    booleanPaths[i] = GeoWrangler.close(booleanPaths[i]);
                }
                catch (Exception)
                {

                }
            }
#if !VARIANCESINGLETHREADED
        );
#endif
        // Scale back down again.
        List<GeoLibPointF[]> booleanGeo = GeoWrangler.pointFsFromPaths(booleanPaths, CentralProperties.scaleFactorForOperation);

        // Process the geometry according to mode, etc.
        // We do this by treating our geometry as a geocore source and calling init with this to set up our instance properties.
        // Feels a little hacky, but it ought to work.
        EntropyLayerSettings tempSettings = new();
        tempSettings.adjustSettings(entropyLayerSettings, gdsOnly: false);
        tempSettings.setInt(EntropyLayerSettings.properties_i.shapeIndex, (int)CommonVars.shapeNames.GEOCORE);
        tempSettings.setInt(EntropyLayerSettings.properties_i.gCSEngine, 1);
        tempSettings.setFileData(booleanGeo.ToList());
        drawnPoly.Clear();
        previewPoints.Clear();
        init(commonVars, chaosSettings, settingsIndex, subShapeIndex, mode, doPASearch, previewMode, currentRow, currentCol, tempSettings, doClockwiseGeoFix: true, process_overlaps: false); // Avoid the baked-in point order reprocessing which breaks our representation.

        processOverlaps(commonVars, settingsIndex, previewPoints, forceOverride:false, (PolyFillType)commonVars.getLayerSettings(settingsIndex).getInt(EntropyLayerSettings.properties_i.fill));
    }

    private void processOverlaps(CommonVars commonVars, int settingsIndex, List<GeoLibPointF[]> sourceData, bool forceOverride = false, PolyFillType pft = PolyFillType.pftNonZero)
    {
        // Filter drawn, process those, then do not-drawn. This allows for element counts to change.
        List<GeoLibPointF[]> drawnStuff = new();
        List<GeoLibPointF[]> notDrawnStuff = new();
        int sCount = sourceData.Count;
        for (int i = 0; i < sCount; i++)
        {
            if (drawnPoly[i])
            {
                drawnStuff.Add(sourceData[i]);
            }
            else
            {
                notDrawnStuff.Add(sourceData[i]);
            }
        }

        double extension = Convert.ToDouble(commonVars.getLayerSettings(settingsIndex)
            .getDecimal(EntropyLayerSettings.properties_decimal.rayExtension));

        double customSizing = 0;

        if (commonVars.getLayerSettings(settingsIndex).getInt(EntropyLayerSettings.properties_i.shapeIndex) ==
            (int) CommonVars.shapeNames.GEOCORE)
        {
            customSizing = GeoWrangler.keyhole_sizing * Convert.ToDouble(commonVars.getLayerSettings(settingsIndex)
                .getDecimal(EntropyLayerSettings.properties_decimal.keyhole_factor));
        }
        
        List<GeoLibPointF[]> processed_Drawn = processOverlaps_core(commonVars, drawnStuff, customSizing:customSizing, extension:extension, forceOverride, pft);

        List<GeoLibPointF[]> processed_NotDrawn = processOverlaps_core(commonVars, notDrawnStuff, customSizing:customSizing, extension: extension, forceOverride, pft);

        previewPoints.Clear();
        drawnPoly.Clear();

        int pdCount = processed_Drawn.Count;
        int pndCount = processed_NotDrawn.Count;

        for (int i = 0; i < pdCount; i++)
        {
            previewPoints.Add(processed_Drawn[i]);
            drawnPoly.Add(true);
        }

        for (int i = 0; i < pndCount; i++)
        {
            previewPoints.Add(processed_NotDrawn[i]);
            drawnPoly.Add(false);
        }

    }

    private List<GeoLibPointF[]> processOverlaps_core(CommonVars commonVars, List<GeoLibPointF[]> sourceData, double customSizing, double extension, bool forceOverride = false, PolyFillType pft = PolyFillType.pftNonZero)
    {
        try
        {
            Clipper c = new() {PreserveCollinear = true};
            Paths sourcePolyData = GeoWrangler.pathsFromPointFs(sourceData, CentralProperties.scaleFactorForOperation);
            Paths resizedPolyData = new();

            // Union isn't always robust, so get a bounding box and run an intersection boolean to rationalize the geometry.
            IntRect bounds = ClipperBase.GetBounds(sourcePolyData);
            Path bounding = new()
            {
                new IntPoint(bounds.left, bounds.bottom),
                new IntPoint(bounds.left, bounds.top),
                new IntPoint(bounds.right, bounds.top),
                new IntPoint(bounds.right, bounds.bottom)
            };

            c.AddPaths(sourcePolyData, PolyType.ptClip, true);
            c.AddPaths(sourcePolyData, PolyType.ptSubject, true);

            c.Execute(ClipType.ctIntersection, resizedPolyData, pft, pft);

            // Avoid the overlap handling if we don't actually need to do it.

            bool returnEarly = false;

            int rpdCount = resizedPolyData.Count;
            int sCount = sourceData.Count;

            if (rpdCount == sCount)
            {
                returnEarly = true;
                for (int i = 0; i < rpdCount; i++)
                {
                    // Clipper drops the closing vertex.
                    if (resizedPolyData[i].Count == sourceData[i].Length - 1)
                    {
                        continue;
                    }

                    returnEarly = false;
                    break;
                }
            }

            if (returnEarly)
            {
                // Secondary check
                c.Clear();

                c.AddPath(bounding, PolyType.ptClip, true);
                c.AddPaths(sourcePolyData, PolyType.ptSubject, true);

                c.Execute(ClipType.ctIntersection, resizedPolyData, pft, pft);

                // Decompose to outers and cutters
                Paths[] decomp = GeoWrangler.getDecomposed(resizedPolyData);

                Paths outers = decomp[(int)GeoWrangler.type.outer];
                Paths cutters = decomp[(int)GeoWrangler.type.cutter];

                int oCount = outers.Count;
                int cCount = cutters.Count;

                // Is any cutter fully enclosed within an outer?
                for (int outer = 0; outer < oCount; outer++)
                {
                    double origArea = Math.Abs(Clipper.Area(outers[outer]));
                    for (int cutter = 0; cutter < cCount; cutter++)
                    {
                        c.Clear();
                        c.AddPath(outers[outer], PolyType.ptSubject, true);
                        c.AddPath(cutters[cutter], PolyType.ptClip, true);
                        Paths test = new();
                        c.Execute(ClipType.ctUnion, test, PolyFillType.pftPositive, PolyFillType.pftPositive);

                        double uArea = test.Sum(t => Math.Abs(Clipper.Area(t)));

                        if (!(Math.Abs(uArea - origArea) < double.Epsilon))
                        {
                            continue;
                        }

                        returnEarly = false;
                        break;
                    }
                    if (!returnEarly)
                    {
                        break;
                    }
                }

                if (returnEarly)
                {
                    return sourceData.ToList();
                }
            }

            resizedPolyData = GeoWrangler.close(resizedPolyData);

            // Here, we can run into trouble....we might have a set of polygons which need to get keyholed. For example, where we have fully enclosed 'cutters' within an outer boundary.
            // Can geoWrangler help us out here?

            // We need to run the fragmenter here because the keyholer / raycaster pipeline needs points for emission.
            Fragmenter f = new(commonVars.getSimulationSettings().getResolution() * CentralProperties.scaleFactorForOperation);
            resizedPolyData = GeoWrangler.makeKeyHole(f.fragmentPaths(resizedPolyData), customSizing:customSizing, extension:extension).ToList();

            if (!resizedPolyData.Any())
            {
                return sourceData;
            }

            // We got some resulting geometry from our Boolean so let's process it to send back to the caller.
            List<GeoLibPointF[]> refinedData = new();

            resizedPolyData = GeoWrangler.close(resizedPolyData);

            rpdCount = resizedPolyData.Count;

            // Switch our fragmenter to use a new configuration for the downsized geometry.
            f = new Fragmenter(commonVars.getSimulationSettings().getResolution(), CentralProperties.scaleFactorForOperation);

            // Convert back our geometry.                
            for (int rPoly = 0; rPoly < rpdCount; rPoly++)
            {
                // We have to re-fragment as the overlap processing changed the geometry heavily.
                refinedData.Add(f.fragmentPath(GeoWrangler.pointFFromPath(resizedPolyData[rPoly], CentralProperties.scaleFactorForOperation)));
            }

            return refinedData.ToList();
        }
        catch (Exception)
        {
            return sourceData.ToList();
        }
    }
}