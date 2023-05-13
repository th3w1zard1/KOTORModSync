// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using KOTORModSync.GUI.Models;
using CommunityToolkit.Mvvm;
using Prism.Commands;

namespace KOTORModSync.GUI_old.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public ObservableCollection<TemplateDataViewModel> TemplateDataList { get; set; }

        public DelegateCommand<IEnumerable<object>> SubmitSelectedTemplatesCommand { get; }

        public MainWindowViewModel()
        {
            TemplateDataList = new ObservableCollection<TemplateDataViewModel>
            {
                new TemplateDataViewModel(new TemplateData("Template 1", TemplateDependencies.Template1Dependencies)),
                new TemplateDataViewModel(new TemplateData("Template 2", TemplateDependencies.Template2Dependencies)),
                new TemplateDataViewModel(new TemplateData("Template 3", TemplateDependencies.Template3Dependencies)),
                new TemplateDataViewModel(new TemplateData("Template 4", TemplateDependencies.Template4Dependencies)),
                new TemplateDataViewModel(new TemplateData("Template 5", TemplateDependencies.Template5Dependencies))
            };

            SubmitSelectedTemplatesCommand = new DelegateCommand<IEnumerable<object>>(SubmitSelectedTemplates);
        }

        private void SubmitSelectedTemplates(IEnumerable<object> items)
        {
            var selectedTemplates = items.Cast<TemplateDataViewModel>()
                                         .Where(x => x.IsSelected)
                                         .Select(x => x.TemplateData)
                                         .ToList();

            // Do something with selected templates...
        }
    }

}
