using System;
using System.Collections.Generic;

namespace MetaProgramming.RoslynCTP.Model
{
    public class ClassTemplateInfo
    {
        public string Name { get; set; }
        public bool IsPublic { get; set; }
        public IDictionary<string, Type> Properties { get; set; }
    }
}
