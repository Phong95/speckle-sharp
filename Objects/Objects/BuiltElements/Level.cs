﻿using Speckle.Core.Kits;
using Speckle.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Objects.BuiltElements
{
  public class Level : Base
  {
    public string name { get; set; }
    public double elevation { get; set; }
    public List<Base> elements { get; set; }

    public Level() { }

    [SchemaInfo("Level", "Creates a Speckle level")]
    public Level(string name, double elevation,
     [SchemaParamInfo("Any nested elements that this floor might have")] List<Base> elements = null)
    {
      this.name = name;
      this.elevation = elevation;
      this.elements = elements;
    }
  }
}

namespace Objects.BuiltElements.Revit
{

  public class RevitLevel : Level
  {
    public bool createView { get; set; }

    public Dictionary<string, object> parameters { get; set; }

    public string elementId { get; set; }

    public bool referenceOnly { get; set; }

    public RevitLevel() { }

    [SchemaInfo("Create level", "Creates a new Revit level unless one with the same name already exists, in which case it edits it")]
    public RevitLevel(string name, double elevation,
      [SchemaParamInfo("If true, it creates an associated view in Revit")] bool createView,
      [SchemaParamInfo("Any nested elements that this floor might have")] List<Base> elements = null,
      Dictionary<string, object> parameters = null)
    {
      this.name = name;
      this.elevation = elevation;
      this.elements = elements;
      this.createView = createView;
      this.parameters = parameters;
      this.referenceOnly = false;
    }

    [SchemaInfo("Level by name", "Gets an existing Revit level by name")]
    public RevitLevel(string name)
    {
      this.name = name;
      this.referenceOnly = true;
    }
  }
}
