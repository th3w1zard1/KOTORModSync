using System;
using System.Collections.Generic;
using System.Windows.Input;
using JetBrains.Annotations;

namespace KOTORModSync.ViewModel
{
    public class RelayCommand : ICommand
    {
        private readonly Func<bool> _canExecute;
        private readonly List<WeakReference<EventHandler>> _canExecuteChangedHandlers;
        private readonly Action _execute;

        public RelayCommand( [CanBeNull] Action execute ) : this( execute, null ) { }

        public RelayCommand( Action execute, [CanBeNull] Func<bool> canExecute )
        {
            _execute = execute ?? throw new ArgumentNullException( nameof( execute ) );
            _canExecute = canExecute;
            _canExecuteChangedHandlers = new List<WeakReference<EventHandler>>();
        }

        public bool CanExecute( [CanBeNull] object parameter ) => _canExecute?.Invoke() ?? true;

        public void Execute( [CanBeNull] object parameter ) => _execute();

        public event EventHandler CanExecuteChanged
        {
            add => AddWeakEventHandler( value );
            remove => RemoveWeakEventHandler( value );
        }

        private void AddWeakEventHandler( [CanBeNull] EventHandler handler )
        {
            _ = _canExecuteChangedHandlers.RemoveAll( h => !h.TryGetTarget( out _ ) );
            _canExecuteChangedHandlers.Add( new WeakReference<EventHandler>( handler ) );
        }

        private void RemoveWeakEventHandler( [CanBeNull] EventHandler handler ) =>
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
