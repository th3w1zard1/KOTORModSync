// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace KOTORModSync.GUI.ViewModel
{
    public class InstallerViewModel : UserControl
    {
        private readonly List<Component> _allComponents;
        private ConfirmationScreenViewModel _confirmationScreenViewModel;
        private InstallationProgressScreenViewModel _installationProgressScreenViewModel;
        private ResultsScreenViewModel _resultsScreenViewModel;
        private SelectComponentsViewModel _selectComponentsViewModel;

        public InstallerViewModel( List<Component> availableComponents )
        {
            InitializeComponent();
            InitializeViewModels();
            DataContext = _selectComponentsViewModel;
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load( this );

        private void InitializeViewModels()
        {
            _selectComponentsViewModel = new SelectComponentsViewModel();
            _confirmationScreenViewModel = new ConfirmationScreenViewModel();
            _installationProgressScreenViewModel = new InstallationProgressScreenViewModel();
            _resultsScreenViewModel = new ResultsScreenViewModel();
        }

        private void ShowScreen( object screenViewModel ) => DataContext = screenViewModel;

        private void NextButton_Click( object sender, RoutedEventArgs e )
        {
            switch ( DataContext )
            {
                case SelectComponentsViewModel _:
                    ShowScreen( _confirmationScreenViewModel );
                    break;
                case ConfirmationScreenViewModel _:
                    ShowScreen( _installationProgressScreenViewModel );
                    break;
                case InstallationProgressScreenViewModel _:
                    ShowScreen( _resultsScreenViewModel );
                    break;
            }
            // Handle navigation to additional screens if needed
        }

        private void BackButton_Click( object sender, RoutedEventArgs e )
        {
            switch ( DataContext )
            {
                case ConfirmationScreenViewModel _:
                    ShowScreen( _selectComponentsViewModel );
                    break;
                case InstallationProgressScreenViewModel _:
                    ShowScreen( _confirmationScreenViewModel );
                    break;
                case ResultsScreenViewModel _:
                    ShowScreen( _installationProgressScreenViewModel );
                    break;
            }
            // Handle navigation to previous screens if needed
        }
    }


    public class SelectComponentsViewModel : INotifyPropertyChanged
    {
        public SelectComponentsViewModel()
        {
            // Initialize collections and commands
            Components = new ObservableCollection<ComponentViewModel>();
            SelectedComponents = new ObservableCollection<ComponentViewModel>();

            NextCommand = new RelayCommand( Next );
        }

        public ObservableCollection<ComponentViewModel> Components { get; set; }
        public ObservableCollection<ComponentViewModel> SelectedComponents { get; set; }

        public ICommand NextCommand { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;

        private void Next()
        {
            // Handle navigation to the next screen
        }
    }

    public class ConfirmationScreenViewModel : INotifyPropertyChanged
    {
        public ConfirmationScreenViewModel()
        {
            // Initialize collections and commands
            SelectedComponents = new ObservableCollection<ComponentViewModel>();

            BackCommand = new RelayCommand( Back );
            NextCommand = new RelayCommand( Next );
        }

        public ObservableCollection<ComponentViewModel> SelectedComponents { get; set; }

        public ICommand BackCommand { get; set; }
        public ICommand NextCommand { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;

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
        public InstallationProgressScreenViewModel() =>
            // Initialize commands
            NextCommand = new RelayCommand( Next );

        public ICommand NextCommand { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;

        private void Next()
        {
            // Handle navigation to the next screen
        }
    }

    public class ResultsScreenViewModel : INotifyPropertyChanged
    {
        public ResultsScreenViewModel() =>
            // Initialize commands
            FinishCommand = new RelayCommand( Finish );

        public ICommand FinishCommand { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;

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
            get => _isSelected;
            set
            {
                if ( _isSelected == value )
                {
                    return;
                }

                _isSelected = value;
                OnPropertyChanged( nameof( IsSelected ) );
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged
            ( string propertyName ) => PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );
    }

    // RelayCommand class for ICommand implementation
    public class RelayCommand : ICommand
    {
        private readonly Func<bool> _canExecute;
        private readonly List<WeakReference<EventHandler>> _canExecuteChangedHandlers;
        private readonly Action _execute;

        public RelayCommand( Action execute )
            : this( execute, null )
        {
        }

        public RelayCommand( Action execute, Func<bool> canExecute )
        {
            _execute = execute ?? throw new ArgumentNullException( nameof( execute ) );
            _canExecute = canExecute;
            _canExecuteChangedHandlers = new List<WeakReference<EventHandler>>();
        }

        public bool CanExecute( object parameter ) => _canExecute?.Invoke() ?? true;

        public void Execute( object parameter ) => _execute();

        public event EventHandler CanExecuteChanged
        {
            add => AddWeakEventHandler( value );
            remove => RemoveWeakEventHandler( value );
        }

        private void AddWeakEventHandler( EventHandler handler )
        {
            _ = _canExecuteChangedHandlers.RemoveAll( h => !h.TryGetTarget( out _ ) );
            _canExecuteChangedHandlers.Add( new WeakReference<EventHandler>( handler ) );
        }

        private void RemoveWeakEventHandler( EventHandler handler ) =>
            _ = _canExecuteChangedHandlers?.RemoveAll(
                h =>
                {
                    if ( !h.TryGetTarget( out EventHandler target ) || target != handler )
                    {
                        return false;
                    }

                    h.SetTarget( null );
                    return true;
                }
            );

        public void RaiseCanExecuteChanged()
        {
            _ = _canExecuteChangedHandlers.RemoveAll( h => !h.TryGetTarget( out _ ) );
            foreach ( WeakReference<EventHandler> weakRef in _canExecuteChangedHandlers )
            {
                if ( !weakRef.TryGetTarget( out EventHandler handler ) )
                {
                    continue;
                }

                handler.Invoke( this, EventArgs.Empty );
            }
        }
    }
}
