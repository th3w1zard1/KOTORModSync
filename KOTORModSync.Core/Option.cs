// Copyright 2021-2023 KOTORModSync
// Licensed under the GNU General Public License v3.0 (GPLv3).
// See LICENSE.txt file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using KOTORModSync.Core.Utility;

namespace KOTORModSync.Core
{
    public sealed class Option : INotifyPropertyChanged
    {
        private bool _isSelected;

        [NotNull] public string Name { get; set; } = string.Empty;
        public Guid Guid { get; set; } = Guid.Empty;
        [NotNull] public List<Guid> Dependencies { get; set; } = new List<Guid>();
        [NotNull] public List<Guid> Restrictions { get; set; } = new List<Guid>();
        [NotNull][ItemNotNull] public List<Instruction> Instructions { get; set; } = new List<Instruction>();

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public string Description { get; set; }

        // used for the ui
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged( [CallerMemberName][CanBeNull] string propertyName = null ) =>
            PropertyChanged?.Invoke( this, new PropertyChangedEventArgs( propertyName ) );

        public void CreateInstruction( int index = 0 )
        {
            var instruction = new Instruction();
            if ( Instructions.IsNullOrEmptyOrAllNull() )
            {
                if ( index != 0 )
                {
                    Logger.LogError( "Cannot create instruction at index when list is empty." );
                    return;
                }

                Instructions.Add( instruction );
            }
            else
            {
                Instructions.Insert( index, instruction );
            }
        }
    }
}
