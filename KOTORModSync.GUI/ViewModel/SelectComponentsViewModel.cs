﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace KOTORModSync.ViewModel
{
    public class InstallerViewModel : UserControl
    {
        private SelectComponentsViewModel selectComponentsViewModel;
        private ConfirmationScreenViewModel confirmationScreenViewModel;
        private InstallationProgressScreenViewModel installationProgressScreenViewModel;
        private ResultsScreenViewModel resultsScreenViewModel;
        private List<Component> allComponents;

        public InstallerViewModel( List<Component> availableComponents )
        {
            InitializeComponent();
            InitializeViewModels();
            DataContext = selectComponentsViewModel;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load( this );
        }

        private void InitializeViewModels()
        {

            selectComponentsViewModel = new SelectComponentsViewModel();
            confirmationScreenViewModel = new ConfirmationScreenViewModel();
            installationProgressScreenViewModel = new InstallationProgressScreenViewModel();
            resultsScreenViewModel = new ResultsScreenViewModel();
        }

        private void ShowScreen( object screenViewModel )
        {
            DataContext = screenViewModel;
        }

        private void NextButton_Click( object sender, RoutedEventArgs e )
        {
            switch ( DataContext )
            {
                case SelectComponentsViewModel _:
                    ShowScreen( confirmationScreenViewModel );
                    break;
                case ConfirmationScreenViewModel _:
                    ShowScreen( installationProgressScreenViewModel );
                    break;
                case InstallationProgressScreenViewModel _:
                    ShowScreen( resultsScreenViewModel );
                    break;
            }
            // Handle navigation to additional screens if needed
        }

        private void BackButton_Click( object sender, RoutedEventArgs e )
        {
            switch ( DataContext )
            {
                case ConfirmationScreenViewModel _:
                    ShowScreen( selectComponentsViewModel );
                    break;
                case InstallationProgressScreenViewModel _:
                    ShowScreen( confirmationScreenViewModel );
                    break;
                case ResultsScreenViewModel _:
                    ShowScreen( installationProgressScreenViewModel );
                    break;
            }
            // Handle navigation to previous screens if needed
        }
    }


    public class SelectComponentsViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<ComponentViewModel> Components { get; set; }
        public ObservableCollection<ComponentViewModel> SelectedComponents { get; set; }

        public ICommand NextCommand { get; set; }

        public SelectComponentsViewModel()
        {
            // Initialize collections and commands
            Components = new ObservableCollection<ComponentViewModel>();
            SelectedComponents = new ObservableCollection<ComponentViewModel>();

            NextCommand = new RelayCommand( Next );
        }

        private void Next()
        {
            // Handle navigation to the next screen
        }
    }

    public class ConfirmationScreenViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<ComponentViewModel> SelectedComponents { get; set; }

        public ICommand BackCommand { get; set; }
        public ICommand NextCommand { get; set; }

        public ConfirmationScreenViewModel()
        {
            // Initialize collections and commands
            SelectedComponents = new ObservableCollection<ComponentViewModel>();

            BackCommand = new RelayCommand( Back );
            NextCommand = new RelayCommand( Next );
        }

        private void Back()
        {
            // Handle navigation to the previous screen
        }

        private void Next()
        {
            // Handle navigation to the next screen
        }
    }

    public class InstallationProgressScreenViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand NextCommand { get; set; }

        public InstallationProgressScreenViewModel()
        {
            // Initialize commands
            NextCommand = new RelayCommand( Next );
        }

        private void Next()
        {
            // Handle navigation to the next screen
        }
    }

    public class ResultsScreenViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public ICommand FinishCommand { get; set; }

        public ResultsScreenViewModel()
        {
            // Initialize commands
            FinishCommand = new RelayCommand( Finish );
        }

        private void Finish()
        {
            // Handle finalization of the installation process
        }
    }

    public class ComponentViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Name { get; set; }

        public bool IsSelected
        {
            get { return _isSelected; }
            set
            {
                if ( _isSelected == value )
                    return;

                _isSelected = value;
                OnPropertyChanged( nameof( IsSelected ) );
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged( string propertyName )
        {
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
        }
    }

    // RelayCommand class for ICommand implementation
    public class RelayCommand : ICommand
    {
        private readonly Action execute;
        private readonly Func<bool> canExecute;
        private List<WeakReference<EventHandler>> canExecuteChangedHandlers;

        public RelayCommand( Action execute )
            : this( execute, null )
        {
        }

        public RelayCommand( Action execute, Func<bool> canExecute )
        {
            this.execute = execute ?? throw new ArgumentNullException( nameof( execute ) );
            this.canExecute = canExecute;
            this.canExecuteChangedHandlers = new List<WeakReference<EventHandler>>();
        }

        public bool CanExecute( object parameter )
        {
            return canExecute?.Invoke() ?? true;
        }

        public void Execute( object parameter )
        {
            execute();
        }

        public event EventHandler CanExecuteChanged
        {
            add
            {
                AddWeakEventHandler( value );
            }
            remove
            {
                RemoveWeakEventHandler( value );
            }
        }

        private void AddWeakEventHandler( EventHandler handler )
        {
            canExecuteChangedHandlers.RemoveAll( h => !h.TryGetTarget( out _ ) );
            canExecuteChangedHandlers.Add( new WeakReference<EventHandler>( handler ) );
        }

        private void RemoveWeakEventHandler( EventHandler handler )
        {
            _ = ( canExecuteChangedHandlers?.RemoveAll(
                h =>
                {
                    if ( !h.TryGetTarget( out EventHandler target ) || target != handler )
                        return false;

                    h.SetTarget( null );
                    return true;
                }
            ) );
        }

        public void RaiseCanExecuteChanged()
        {
            canExecuteChangedHandlers.RemoveAll( h => !h.TryGetTarget( out _ ) );
            foreach ( WeakReference<EventHandler> weakRef in canExecuteChangedHandlers )
            {
                if ( !weakRef.TryGetTarget( out EventHandler handler ) )
                    continue;

                handler.Invoke( this, EventArgs.Empty );
            }
        }
    }
}