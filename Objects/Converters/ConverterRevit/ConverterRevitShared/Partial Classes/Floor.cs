﻿
using Autodesk.Revit.DB;
using Objects.BuiltElements.Revit;
using Objects.Geometry;
using Speckle.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using DB = Autodesk.Revit.DB;

namespace Objects.Converter.Revit
{
  public partial class ConverterRevit
  {
    public List<ApplicationPlaceholderObject> FloorToNative(BuiltElements.Floor speckleFloor)
    {
      if (speckleFloor.outline == null)
      {
        throw new Exception("Only outline based Floor are currently supported.");
      }

      bool structural = false;
      var outline = CurveToNative(speckleFloor.outline);

      Level level;
      if (speckleFloor is RevitFloor speckleRevitFloor)
      {
        level = LevelToNative(speckleRevitFloor.level);
        structural = speckleRevitFloor.structural;
      }
      else
      {
        level = LevelToNative(LevelFromCurve(outline.get_Item(0)));
      }

      var floorType = GetElementType<FloorType>(speckleFloor);

      // NOTE: I have not found a way to edit a slab outline properly, so whenever we bake, we renew the element.
      var docObj = GetExistingElementByApplicationId(speckleFloor.applicationId);
      if (docObj != null)
      {
        Doc.Delete(docObj.Id);
      }

      Floor revitFloor;
      if (floorType == null)
      {
        revitFloor = Doc.Create.NewFloor(outline, structural);
      }
      else
      {
        revitFloor = Doc.Create.NewFloor(outline, floorType, level, structural);
      }

      Doc.Regenerate();

      try
      {
        CreateOpenings(revitFloor, speckleFloor.voids);
      }
      catch (Exception ex)
      {
        ConversionErrors.Add(new Error($"Could not create openings in floor {speckleFloor.applicationId}", ex.Message));
      }

      SetElementParamsFromSpeckle(revitFloor, speckleFloor);

      var placeholders = new List<ApplicationPlaceholderObject>() { new ApplicationPlaceholderObject { applicationId = speckleFloor.applicationId, ApplicationGeneratedId = revitFloor.UniqueId, NativeObject = revitFloor } };

      // TODO: nested elements.

      return placeholders;
    }

    private void CreateOpenings(DB.Floor floor, List<ICurve> holes)
    {
      foreach (var hole in holes)
      {
        var curveArray = CurveToNative(hole);
        Doc.Create.NewOpening(floor, curveArray, true);
      }
    }

    private RevitFloor FloorToSpeckle(DB.Floor revitFloor)
    {
      var baseLevelParam = revitFloor.get_Parameter(BuiltInParameter.LEVEL_PARAM);
      var structuralParam = revitFloor.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL);
      var profiles = GetProfiles(revitFloor);

      var speckleFloor = new RevitFloor();
      speckleFloor.type = Doc.GetElement(revitFloor.GetTypeId()).Name;
      speckleFloor.outline = profiles[0];
      if (profiles.Count > 1)
      {
        speckleFloor.voids = profiles.Skip(1).ToList();
      }

      speckleFloor.level = ConvertAndCacheLevel(baseLevelParam);
      speckleFloor.structural = (bool)ParameterToSpeckle(structuralParam);

      AddCommonRevitProps(speckleFloor, revitFloor);

      var mesh = new Geometry.Mesh();
      (mesh.faces, mesh.vertices) = GetFaceVertexArrayFromElement(revitFloor, new Options() { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = false });

      speckleFloor["@displayMesh"] = mesh;

      // TODO
      var hostedElements = revitFloor.FindInserts(true, true, true, true);

      return speckleFloor;
    }

    //Nesting the various profiles into a polycurve segments
    private List<ICurve> GetProfiles(DB.CeilingAndFloor floor)
    {
      var profiles = new List<ICurve>();
      var faces = HostObjectUtils.GetTopFaces(floor);
      Face face = floor.GetGeometryObjectFromReference(faces[0]) as Face;
      var crvLoops = face.GetEdgesAsCurveLoops();
      foreach (var crvloop in crvLoops)
      {
        var poly = new Polycurve(ModelUnits);
        foreach (var curve in crvloop)
        {
          var c = curve;

          if (c == null)
          {
            continue;
          }

          poly.segments.Add(CurveToSpeckle(c));
        }
        profiles.Add(poly);
      }
      return profiles;
    }
  }
}
