using System;
using System.Collections.Generic;
using System.Linq;
using geoLib;

namespace Variance;

public class Results
{
    private List<List<GeoLibPointF[]>> previewShapes;
    private List<PreviewShape> simShapes;

    public int getLayerIndex(int shapeIndex)
    {
        return pGetLayerIndex(shapeIndex);
    }

    private int pGetLayerIndex(int shapeIndex)
    {
        return simShapes[shapeIndex].getIndex();
    }
    public List<List<GeoLibPointF[]>> getPreviewShapes()
    {
        return pGetPreviewShapes();
    }

    private List<List<GeoLibPointF[]>> pGetPreviewShapes()
    {
        return previewShapes;
    }

    public void clearPreviewShapes()
    {
        pClearPreviewShapes();
    }

    private void pClearPreviewShapes()
    {
        previewShapes.Clear();
    }

    public void setPreviewShapes(List<List<GeoLibPointF[]>> newPreviewShapes)
    {
        pSetPreviewShapes(newPreviewShapes);
    }

    private void pSetPreviewShapes(List<List<GeoLibPointF[]>> newPreviewShapes)
    {
        previewShapes = newPreviewShapes.ToList();
    }

    public List<PreviewShape> getSimShapes()
    {
        return pGetSimShapes();
    }

    private List<PreviewShape> pGetSimShapes()
    {
        return simShapes;
    }

    public void setSimShapes(List<PreviewShape> newSimShapes)
    {
        pSetSimShapes(newSimShapes);
    }

    private void pSetSimShapes(List<PreviewShape> newSimShapes)
    {
        simShapes = newSimShapes.ToList();
    }

    public void clearSimShapes()
    {
        pClearSimShapes();
    }

    private void pClearSimShapes()
    {
        simShapes.Clear();
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

    public void setResult(string val)
    {
        pSetResult(val);
    }

    private void pSetResult(string val)
    {
        result = val;
    }

    private List<GeoLibPointF[]> points; // result points.

    public List<GeoLibPointF[]> getPoints()
    {
        return pGetPoints();
    }

    private List<GeoLibPointF[]> pGetPoints()
    {
        return points;
    }

    public void setPoints(List<GeoLibPointF[]> newPoints)
    {
        pSetPoints(newPoints);
    }

    private void pSetPoints(List<GeoLibPointF[]> newPoints)
    {
        points = newPoints.ToList();
    }

    public void clearPoints()
    {
        pClearPoints();
    }

    private void pClearPoints()
    {
        points.Clear();
    }

    public enum fields_d { svar, tvar, lwr, lwr2, htip, vtip, icv, ocv, olx, oly, wob }

    private double[] CDUSVar;
    private double[] CDUTVar;
    private double[] LWRVar;
    private double[] LWR2Var;
    private double[] horTipBiasVar;
    private double[] verTipBiasVar;
    private double[] iCVar;
    private double[] oCVar;
    private double[] overlayX;
    private double[] overlayY;
    private double[] wobbleVar;

    public double[] getFields(fields_d f)
    {
        return pGetFields(f);
    }

    private double[] pGetFields(fields_d f)
    {
        double[] ret = { };
        switch (f)
        {
            case fields_d.svar:
                ret = CDUSVar;
                break;
            case fields_d.tvar:
                ret = CDUTVar;
                break;
            case fields_d.lwr:
                ret = LWRVar;
                break;
            case fields_d.lwr2:
                ret = LWR2Var;
                break;
            case fields_d.htip:
                ret = horTipBiasVar;
                break;
            case fields_d.vtip:
                ret = verTipBiasVar;
                break;
            case fields_d.icv:
                ret = iCVar;
                break;
            case fields_d.ocv:
                ret = oCVar;
                break;
            case fields_d.olx:
                ret = overlayX;
                break;
            case fields_d.oly:
                ret = overlayY;
                break;
            case fields_d.wob:
                ret = wobbleVar;
                break;
        }

        return ret;
    }

    public double getField(fields_d f, int index)
    {
        return pGetField(f, index);
    }

    private double pGetField(fields_d f, int index)
    {
        double[] t = pGetFields(f);
        return t[index];
    }

    public void setFields(fields_d f, double[] val)
    {
        pSetFields(f, val);
    }

    private void pSetFields(fields_d f, double[] val)
    {
        switch (f)
        {
            case fields_d.svar:
                CDUSVar = val;
                break;
            case fields_d.tvar:
                CDUTVar = val;
                break;
            case fields_d.lwr:
                LWRVar = val;
                break;
            case fields_d.lwr2:
                LWR2Var = val;
                break;
            case fields_d.htip:
                horTipBiasVar = val;
                break;
            case fields_d.vtip:
                verTipBiasVar = val;
                break;
            case fields_d.icv:
                iCVar = val;
                break;
            case fields_d.ocv:
                oCVar = val;
                break;
            case fields_d.olx:
                overlayX = val;
                break;
            case fields_d.oly:
                overlayY = val;
                break;
            case fields_d.wob:
                wobbleVar = val;
                break;
        }
    }

    public void setField(fields_d f, int index, double val)
    {
        pSetField(f, index, val);
    }

    private void pSetField(fields_d f, int index, double val)
    {
        double[] t = pGetFields(f);
        t[index] = val;
    }

    public enum fields_i { lwrs, lwr2s }

    private int[] LWRSeed;
    private int[] LWR2Seed;

    public int[] getSeeds(fields_i f)
    {
        return pGetSeeds(f);
    }

    private int[] pGetSeeds(fields_i f)
    {
        int[] ret = { };
        switch (f)
        {
            case fields_i.lwrs:
                ret = LWRSeed;
                break;
            case fields_i.lwr2s:
                ret = LWR2Seed;
                break;
        }

        return ret;
    }

    public int getSeed(fields_i f, int index)
    {
        return pGetSeed(f, index);
    }

    private int pGetSeed(fields_i f, int index)
    {
        int[] t = pGetSeeds(f);
        return t[index];
    }

    public void setSeeds(fields_i f, int[] val)
    {
        pSetSeeds(f, val);
    }

    private void pSetSeeds(fields_i f, int[] val)
    {
        switch (f)
        {
            case fields_i.lwrs:
                LWRSeed = val;
                break;
            case fields_i.lwr2s:
                LWR2Seed = val;
                break;
        }
    }


    private bool valid;

    public void setValid(bool val)
    {
        pSetValid(val);
    }

    private void pSetValid(bool val)
    {
        valid = val;
    }

    public bool isValid()
    {
        return pIsValid();
    }

    private bool pIsValid()
    {
        return valid;
    }

    public Results()
    {
        init();
    }

    private void init()
    {
        LWRSeed = new int[] { };
        LWR2Seed = new int[] { };

        previewShapes = new List<List<GeoLibPointF[]>>();
        valid = false;
    }

    public void genPreviewShapes()
    {
        pGenPreviewShapes();
    }

    private void pGenPreviewShapes()
    {
        previewShapes.Clear();
        for (int layer = 0; layer < CentralProperties.maxLayersForMC; layer++)
        {
            previewShapes.Add(simShapes[layer].getPoints().ToList());
        }
    }
}