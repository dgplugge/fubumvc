using System;
using System.Collections.Generic;
using System.Linq;
using FubuCore;
using FubuCore.Descriptions;
using FubuMVC.Core.Registration.ObjectGraph;

namespace FubuMVC.Core.Resources.Conneg
{
    public class Writer : WriterNode, DescribesItself
    {
        private readonly Type _resourceType;
        private readonly Type _writerType;

        public Writer(Type writerType, Type resourceType = null)
        {
            if (writerType == null)
            {
                throw new ArgumentNullException("writerType");
            }

            if (writerType.IsOpenGeneric())
            {
                if (resourceType == null)
                {
                    throw new ArgumentNullException("resourceType", "resourceType is required if the writerType is an open generic");
                }

                _resourceType = resourceType;
                _writerType = writerType.MakeGenericType(resourceType);
            }
            else
            {
                var @interface = writerType.FindInterfaceThatCloses(typeof (IMediaWriter<>));
                if (@interface == null)
                {
                    throw new ArgumentOutOfRangeException("writerType", "writerType must be assignable to IMediaWriter<>");
                }

                _writerType = writerType;
                _resourceType = @interface.GetGenericArguments().First();
            }

            
        }

        public override Type ResourceType
        {
            get { return _resourceType; }
        }

        public Type WriterType
        {
            get { return _writerType; }
        }

        protected override ObjectDef toWriterDef()
        {
            return new ObjectDef(WriterType);
        }

        public override IEnumerable<string> Mimetypes
        {
            get { return MimeTypeAttribute.ReadFrom(WriterType); }
        }

        void DescribesItself.Describe(Description description)
        {
            description.Title = _writerType.Name;
            description.ShortDescription = "Writes the resource model {0} with {1}".ToFormat(_resourceType.FullName,
                                                                                            _writerType.FullName);
        }
    }
}