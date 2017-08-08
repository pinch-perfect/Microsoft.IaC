﻿using InfrastructureAsCode.Powershell.CmdLets;
using InfrastructureAsCode.Powershell.PipeBinds;
using Microsoft.SharePoint.Client;
using System;
using System.Linq;
using System.Management.Automation;

namespace InfrastructureAsCode.Powershell.Commands.Workflows
{
    [Cmdlet(VerbsLifecycle.Resume, "IaCWorkflowInstance")]
    public class ResumeIaCWorkflowInstance : IaCCmdlet
    {
        [Parameter(Mandatory = true, HelpMessage = "The instance to resume", Position = 0)]
        public WorkflowInstancePipeBind Identity;

        public override void ExecuteCmdlet()
        {
            if (Identity.Instance != null)
            {
                Identity.Instance.ResumeWorkflow();
            }
            else if (Identity.Id != Guid.Empty)
            {
                var SelectedWeb = this.ClientContext.Web;

                var allinstances = SelectedWeb.GetWorkflowInstances();
                foreach (var instance in allinstances.Where(instance => instance.Id == Identity.Id))
                {
                    instance.ResumeWorkflow();
                    break;
                }
            }
        }
    }


}