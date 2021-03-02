﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace MDD4All.SpecIF.DataModels.DiagramInterchange.BaseElements
{
    public abstract class SpecIfDiagramInterchangeBase
    {
        [XmlAttribute("id")]
        public string ID { get; set; } = null;
    }
}
