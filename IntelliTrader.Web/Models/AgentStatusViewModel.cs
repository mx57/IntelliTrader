using System;
using System.Collections.Generic;

namespace IntelliTrader.Web.Models
{
    public class AgentStatusViewModel : BaseViewModel
    {
        public int EpisodicMemoryCount { get; set; }
        public double Energy { get; set; }
        public double Boredom { get; set; }
        public List<AgentTask> ActiveTasks { get; set; }
        public List<AgentTask> AllTasks { get; set; }
    }

    public class AgentTask
    {
        public string Id { get; set; }
        public string Status { get; set; }
        public string Area { get; set; }
        public string Risk { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public List<string> AllowedPaths { get; set; }
        public List<string> Acceptance { get; set; }
    }

    public class AgentTasksData
    {
        public int SchemaVersion { get; set; }
        public string Project { get; set; }
        public List<AgentTask> Tasks { get; set; }
    }
}
