﻿using Speckle.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Speckle.Core.Kits
{

  public interface ISpeckleConverter
  {
    string Description { get; }
    string Name { get; }
    string Author { get; }
    string WebsiteOrEmail { get; }
    /// <summary>
    /// Converts a native object to a Speckle one
    /// </summary>
    /// <param name="object">Native object to convert</param>
    /// <returns></returns>
    public Base ConvertToSpeckle(object @object);

    /// <summary>
    /// Converts a list of native objects to a list Speckle ones
    /// </summary>
    /// <param name="objects">List of native objects to convert</param>
    /// <returns></returns>
    public IEnumerable<Base> ConvertBatchToSpeckle(IEnumerable<object> objects);

    /// <summary>
    /// Checks if it can onvert a native object to a Speckle one
    /// </summary>
    /// <param name="object">Native object to convert</param>
    /// <returns></returns>
    public bool CanConvertToSpeckle(object @object);

    /// <summary>
    /// Converts a Speckle object to a native one
    /// </summary>
    /// <param name="object">Speckle object to convert</param>
    /// <returns></returns>
    public object ConvertToNative(Base @object);

    /// <summary>
    /// Converts a list of Speckle objects to a list native ones
    /// </summary>
    /// <param name="objects">List of Speckle objects to convert</param>
    /// <returns></returns>
    public IEnumerable<object> ConvertBatchToNative(IEnumerable<Base> objects);

    /// <summary>
    /// Checks if it can convert a Speckle object to a native one
    /// </summary>
    /// <param name="object">Speckle object to convert</param>
    /// <returns></returns>
    public bool CanConvertToNative(Base @object);

    /// <summary>
    /// Returns a list of applicaitons serviced by this converter
    /// </summary>
    /// <returns></returns>
    public IEnumerable<string> GetServicedApplications();
  }
}
