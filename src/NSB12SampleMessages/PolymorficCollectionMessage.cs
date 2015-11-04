using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace NSB12SampleMessages
{
    [KnownType(typeof(SpecializationA))]
    [KnownType(typeof(SpecializationB))]
    public class PolymorficCollectionMessage
    {
        public List<BaseEntity> Items { get; set; }
    }

    public abstract class BaseEntity
    {
        public virtual string Name { get; set; }
    }

    public class SpecializationA : BaseEntity
    {
        public override string Name { get; set; }

        public override bool Equals(object obj)
        {
            var other = (SpecializationA)obj;

            return other != null && this.Name == other.Name;
        }

        public override int GetHashCode()
        {
            return Name?.GetHashCode() ?? 0;
        }
    }

    public class SpecializationB : BaseEntity
    {
        public override string Name { get; set; }

        public override bool Equals(object obj)
        {
            var other = (SpecializationB)obj;

            return other != null && this.Name == other.Name;
        }

        public override int GetHashCode()
        {
            return Name?.GetHashCode() ?? 0;
        }
    }
}