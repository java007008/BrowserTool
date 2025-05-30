using System.Collections.Generic;
using System.Collections.ObjectModel;
using BrowserTool.Database.Entities;
using BrowserTool.Database;

namespace BrowserTool.ViewModels
{
    public class SiteGroupViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int SiteCount { get; set; }
        public ObservableCollection<SiteItem> Sites { get; set; }

        public SiteGroupViewModel(SiteGroup group)
        {
            Id = group.Id;
            Name = group.Name;
            Sites = new ObservableCollection<SiteItem>(SiteConfig.GetSitesByGroup(group.Id));
            SiteCount = Sites.Count;
        }
    }
} 