﻿using System;
using System.Xml;
using System.Xml.Serialization;
using Utils = InfrastructureAsCode.Core.Extensions.StringExtensions;

namespace InfrastructureAsCode.Core.Reports.o365rwsclient.TenantReport
{
    [Serializable]
    public class StaleMailbox : TenantReportObject
    {
        [XmlElement]
        public int ActiveMailboxes
        {
            get;
            set;
        }

        [XmlElement]
        public int InactiveMailboxes31To60Days
        {
            get;
            set;
        }

        [XmlElement]
        public int InactiveMailboxes61To90Days
        {
            get;
            set;
        }

        [XmlElement]
        public int InactiveMailboxes91To1460Days
        {
            get;
            set;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="node"></param>
        public override void LoadFromXml(XmlNode node)
        {
            base.LoadFromXml(node);
            ActiveMailboxes = Utils.TryParseInt(base.TryGetValue("ActiveMailboxes"), 0);
            InactiveMailboxes31To60Days = Utils.TryParseInt(base.TryGetValue("InactiveMailboxes31To60Days"), 0);
            InactiveMailboxes61To90Days = Utils.TryParseInt(base.TryGetValue("InactiveMailboxes61To90Days"), 0);
            InactiveMailboxes91To1460Days = Utils.TryParseInt(base.TryGetValue("InactiveMailboxes91To1460Days"), 0);
        }
    }
}