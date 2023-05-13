// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace KOTORModSync.GUI_old.Models
{
    public class TemplateData
    {
        public string TemplateName { get; set; }
        public List<string> Dependencies { get; set; }
        public bool IsSelected { get; set; }
    }
}

