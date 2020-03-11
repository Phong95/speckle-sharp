﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CSharp.RuntimeBinder;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Speckle.Kits;
using Speckle.Models;
using Speckle.Transports;

namespace Speckle.Serialisation
{
  internal static class SerializationUtilities
  {
    #region Getting Types

    private static Dictionary<string, Type> cachedTypes = new Dictionary<string, Type>();

    internal static Type GetType(string objFullType)
    {
      var objectTypes = objFullType.Split(':').Reverse();

      if (cachedTypes.ContainsKey(objectTypes.First()))
        return cachedTypes[objectTypes.First()];

      foreach (var typeName in objectTypes)
      {
        var type = KitManager.Types.FirstOrDefault(tp => tp.FullName == typeName);
        if (type != null)
        {
          cachedTypes[typeName] = type;
          return type;
        }
      }

      return typeof(Base);
    }

    #endregion

    #region value handling

    internal static object HandleValue(JToken value, Newtonsoft.Json.JsonSerializer serializer, JsonProperty jsonProperty = null, string TypeDiscriminator = "speckle_type")
    {
      if (value is JValue)
      {
        return ((JValue)value).Value;
      }

      if (value is JArray)
      {
        if (jsonProperty != null && jsonProperty.PropertyType.GetConstructor(Type.EmptyTypes) != null)
        {
          var arr = jsonProperty != null ? Activator.CreateInstance(jsonProperty.PropertyType) : new List<object>();
          foreach (var val in ((JArray)value))
          {
            ((IList)arr).Add(HandleValue(val, serializer));
          }
          return arr;
        }
        else if (jsonProperty != null)
        {
          var arr = Activator.CreateInstance(typeof(List<>).MakeGenericType(jsonProperty.PropertyType.GetElementType()));
          var actualArr = Array.CreateInstance(jsonProperty.PropertyType.GetElementType(), ((JArray)value).Count);

          foreach (var val in ((JArray)value))
          {
            ((IList)arr).Add(Convert.ChangeType(HandleValue(val, serializer), jsonProperty.PropertyType.GetElementType()));
          }

          ((IList)arr).CopyTo(actualArr, 0);
          return actualArr;
        }
        else
        {
          var arr = new List<object>();
          foreach (var val in ((JArray)value))
          {
            arr.Add(HandleValue(val, serializer));
          }
          return arr;
        }
      }

      if (value is JObject)
      {
        if (((JObject)value).Property(TypeDiscriminator) != null)
        {
          return value.ToObject<Base>(serializer);
        }

        var dict = jsonProperty != null ? Activator.CreateInstance(jsonProperty.PropertyType) : new Dictionary<string, object>();
        foreach (var prop in ((JObject)value))
        {
          object key = prop.Key;
          if (jsonProperty != null)
            key = Convert.ChangeType(prop.Key, jsonProperty.PropertyType.GetGenericArguments()[0]);
          ((IDictionary)dict)[key] = HandleValue(prop.Value, serializer);
        }
        return dict;
      }
      return null;
    }

    #endregion

    #region Abstract Handling

    private static Dictionary<string, Type> cachedAbstractTypes = new Dictionary<string, Type>();

    internal static object HandleAbstractOriginalValue(JToken jToken, string assemblyQualifiedName, Newtonsoft.Json.JsonSerializer serializer)
    {
      if (cachedAbstractTypes.ContainsKey(assemblyQualifiedName))
        return jToken.ToObject(cachedAbstractTypes[assemblyQualifiedName]);

      var pieces = assemblyQualifiedName.Split(',').Select(s => s.Trim()).ToArray();

      var myAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(ass => ass.GetName().Name == pieces[1]);
      if (myAssembly == null) throw new Exception("Could not load abstract object's assembly.");

      var myType = myAssembly.GetType(pieces[0]);
      if (myType == null) throw new Exception("Could not load abstract object's assembly.");

      cachedAbstractTypes[assemblyQualifiedName] = myType;

      return jToken.ToObject(myType);
    }

    #endregion
  }

  internal static class CallSiteCache
  {
    // Adapted from the answer to 
    // https://stackoverflow.com/questions/12057516/c-sharp-dynamicobject-dynamic-properties
    // by jbtule, https://stackoverflow.com/users/637783/jbtule
    // And also
    // https://github.com/mgravell/fast-member/blob/master/FastMember/CallSiteCache.cs
    // by Marc Gravell, https://github.com/mgravell

    private static readonly Dictionary<string, CallSite<Func<CallSite, object, object, object>>> setters
      = new Dictionary<string, CallSite<Func<CallSite, object, object, object>>>();

    public static void SetValue(string propertyName, object target, object value)
    {
      CallSite<Func<CallSite, object, object, object>> site;

      lock (setters)
      {
        if (!setters.TryGetValue(propertyName, out site))
        {
          var binder = Microsoft.CSharp.RuntimeBinder.Binder.SetMember(CSharpBinderFlags.None,
               propertyName, typeof(CallSiteCache),
               new List<CSharpArgumentInfo>{
                   CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
                   CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null)});
          setters[propertyName] = site = CallSite<Func<CallSite, object, object, object>>.Create(binder);
        }
      }

      site.Target(site, target, value);
    }
  }
}