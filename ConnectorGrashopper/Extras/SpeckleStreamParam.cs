﻿using System;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Speckle.Core.Api;
using Speckle.Core.Credentials;

namespace ConnectorGrashopper.Extras
{
    public class SpeckleStreamParam: GH_Param<GH_SpeckleStream>
    {
        public SpeckleStreamParam(IGH_InstanceDescription tag) : base(tag)
        {
        }

        public SpeckleStreamParam(IGH_InstanceDescription tag, GH_ParamAccess access) : base(tag, access)
        {
        }

        public SpeckleStreamParam(string name, string nickname, string description, string category, string subcategory, GH_ParamAccess access) : base(name, nickname, description, category, subcategory, access)
        {
        }
        
        public SpeckleStreamParam(string name, string nickname, string description, GH_ParamAccess access) : base(name, nickname, description, "Speckle 2", "Params", access)
        {
        }
        protected override GH_SpeckleStream PreferredCast(object data)
        {
            if(data is StreamWrapper wrapper) return new GH_SpeckleStream(wrapper);
            return base.PreferredCast(data);
        }

        public override Guid ComponentGuid => new Guid("FB436A31-1CE9-413C-B524-8A574C0F842D");
        
    }

    public sealed class GH_SpeckleStream : GH_Goo<StreamWrapper>
    {
        
        public static implicit operator StreamWrapper(GH_SpeckleStream d) => d.Value;
        

        public override StreamWrapper Value { get; set; }

        public GH_SpeckleStream()
        {
            Value = null;
        }
        public GH_SpeckleStream(GH_Goo<StreamWrapper> other) : base(other)
        {
            Value = other.Value;
        }

        public GH_SpeckleStream(StreamWrapper internal_data) : base(internal_data)
        {
            Value = internal_data;
        }

        public override IGH_Goo Duplicate()
        {
            return new GH_SpeckleStream(Value);
        }

        public override string ToString()
        {
            return Value != null? Value.ToString()  : "Empty speckle stream";
        }

        public override bool CastTo<Q>(ref Q target)
        {
            if (typeof(Q) != typeof(StreamWrapper)) return base.CastTo(ref target);

            target = (Q)(object)Value;
            return true;
        }

        public override bool CastFrom(object source)
        {
            var wrapper = (StreamWrapper) source;
            if (wrapper == null) return base.CastFrom(source);
            Value = wrapper;
            return true;
        }

        public override bool IsValid => Value != null;
        public override string TypeName => "StreamParam";
        public override string TypeDescription =>  "A speckle data stream";
    }
}