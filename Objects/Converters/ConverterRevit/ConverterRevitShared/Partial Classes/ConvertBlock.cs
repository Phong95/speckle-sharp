﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Autodesk.Revit.DB;
using ConverterRevitShared.Revit;
using Objects.Geometry;
using Speckle.Core.Logging;
using Speckle.Core.Models;
using DB = Autodesk.Revit.DB;
using DirectShape = Objects.BuiltElements.Revit.DirectShape;
using Mesh = Objects.Geometry.Mesh;
using Parameter = Objects.BuiltElements.Revit.Parameter;
using BlockInstance = Objects.Other.BlockInstance;
using BlockDefinition = Objects.Other.BlockDefinition;

namespace Objects.Converter.Revit
{
  public partial class ConverterRevit
  {
    public List<ApplicationPlaceholderObject> BlockInstanceToNative(BlockInstance instance)
    {
      // Get or make family from block definition
      FamilySymbol familySymbol = new FilteredElementCollector(Doc)
        .OfClass(typeof(Family))
        .OfType<Family>()
        .FirstOrDefault(f => f.Name.Equals("SpeckleBlock_" + instance.blockDefinition.name))
        ?.GetFamilySymbolIds()
        .Select(id => Doc.GetElement(id))
        .OfType<FamilySymbol>()
        .First();

      if (familySymbol == null)
        familySymbol = BlockDefinitionToNative(instance.blockDefinition);

      // base point
      var basePoint = PointToNative(instance.insertionPoint);

      FamilyInstance _instance = Doc.Create.NewFamilyInstance(basePoint, familySymbol, DB.Structure.StructuralType.NonStructural);

      Doc.Regenerate();

      // transform
      if (MatrixDecompose(instance.transform, out double rotation))
      {
        try
        {
          // some point based families don't have a rotation, so keep this in a try catch
          if (rotation != (_instance.Location as LocationPoint).Rotation)
          {
            var axis = DB.Line.CreateBound(new XYZ(basePoint.X, basePoint.Y, 0), new XYZ(basePoint.X, basePoint.Y, 1000));
            (_instance.Location as LocationPoint).Rotate(axis, rotation - (_instance.Location as LocationPoint).Rotation);
          }
        }
        catch { }

      }

      SetInstanceParameters(_instance, instance);

      var placeholders = new List<ApplicationPlaceholderObject>()
      {
        new ApplicationPlaceholderObject
        {
        applicationId = instance.applicationId,
        ApplicationGeneratedId = _instance.UniqueId,
        NativeObject = _instance
        }
      };

      return placeholders;
    }

    // TODO: fix unit conversions since block geometry is being converted inside a new family document, which potentially has different unit settings from the main doc.
    // This could be done by passing in an option Document argument for all conversions that defaults to the main doc (annoying)
    // I suspect this also needs to be fixed for freeform elements
    private FamilySymbol BlockDefinitionToNative(BlockDefinition definition)
    {
      // convert definition geometry to native
      var solids = new List<DB.Solid>();
      var curves = new List<DB.Curve>();
      foreach (var geometry in definition.geometry)
      {
        switch (geometry)
        {
          case Brep brep:
            try
            {
              var solid = BrepToNative(geometry as Brep);
              solids.Add(solid);
            }
            catch (Exception e)
            {
              ConversionErrors.Add(new SpeckleException($"Could not convert block {definition.id} brep to native, falling back to mesh representation.", e));
              var brepMeshSolids = MeshToNative(brep.displayMesh, DB.TessellatedShapeBuilderTarget.Solid, DB.TessellatedShapeBuilderFallback.Abort)
                  .Select(m => m as DB.Solid);
              solids.AddRange(brepMeshSolids);
            }
            break;
          case Mesh mesh:
            var meshSolids = MeshToNative(mesh, DB.TessellatedShapeBuilderTarget.Solid, DB.TessellatedShapeBuilderFallback.Abort)
                .Select(m => m as DB.Solid);
            solids.AddRange(meshSolids);
            break;
          case ICurve curve:
            try
            {
              var modelCurves = CurveToNative(geometry as ICurve);
              foreach (DB.Curve modelCurve in modelCurves)
                curves.Add(modelCurve);
            }
            catch (Exception e)
            {
              ConversionErrors.Add(new SpeckleException($"Could not convert block {definition.id} curve to native.", e));
            }
            break;
        }
      }

      var tempPath = CreateBlockFamily(solids, curves, definition.name);
      Doc.LoadFamily(tempPath, new FamilyLoadOption(), out var fam);
      var symbol = Doc.GetElement(fam.GetFamilySymbolIds().First()) as DB.FamilySymbol;
      symbol.Activate();
      try
      {
        File.Delete(tempPath);
      }
      catch
      {
      }

      return symbol;
    }

    private bool MatrixDecompose(double[] m, out double rotation)
    {
      var matrix = new Matrix4x4(
        (float)m[0], (float)m[1], (float)m[2], (float)m[3],
        (float)m[4], (float)m[5], (float)m[6], (float)m[7],
        (float)m[8], (float)m[9], (float)m[10], (float)m[11],
        (float)m[12], (float)m[13], (float)m[14], (float)m[15]);

      if (Matrix4x4.Decompose(matrix, out Vector3 _scale, out Quaternion _rotation, out Vector3 _translation))
      {
        rotation = Math.Acos(_rotation.W) * 2;
        return true;
      }
      else
      {
        rotation = 0;
        return false;
      }
    }

    private string CreateBlockFamily(List<DB.Solid> solids, List<DB.Curve> curves, string name)
    {
      // create a family to represent a block definition
      // TODO: package our own generic model rft so this path will always work (need to change for freeform elem too)
      // TODO: match the rft unit to the main doc unit system (ie if main doc is in feet, pick the English Generic Model)
      // TODO: rename block with stream commit info prefix taken from UI - need to figure out cleanest way of storing this in the doc for retrieval by converter
      var famPath = Path.Combine(Doc.Application.FamilyTemplatePath, @"Metric Generic Model.rft");
      if (!File.Exists(famPath))
      {
        throw new Exception($"Could not find file Metric Generic Model.rft - {famPath}");
      }

      var famDoc = Doc.Application.NewFamilyDocument(famPath);
      using (DB.Transaction t = new DB.Transaction(famDoc, "Create Block Geometry Elements"))
      {
        t.Start();

        solids.ForEach(o => { DB.FreeFormElement.Create(famDoc, o); });
        curves.ForEach(o => { famDoc.FamilyCreate.NewModelCurve(o, NewSketchPlaneFromCurve(o, famDoc)); });

        t.Commit();
      }

      var famName = "SpeckleBlock_" + name;
      string tempFamilyPath = Path.Combine(Path.GetTempPath(), famName + ".rfa");
      var so = new DB.SaveAsOptions();
      so.OverwriteExistingFile = true;
      famDoc.SaveAs(tempFamilyPath, so);
      famDoc.Close();

      return tempFamilyPath;
    }
  }
}