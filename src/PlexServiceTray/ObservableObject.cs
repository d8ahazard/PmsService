using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace PlexServiceTray
{
    public abstract class ObservableObject : INotifyPropertyChanged, IDataErrorInfo
    {
        protected object? ValidationContext { get; init; }

        internal bool IsSelectedInternal;

        public virtual bool IsSelected
        {
            get => IsSelectedInternal;
            set 
            {
                if (IsSelectedInternal == value) return;

                IsSelectedInternal = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        private bool _isExpanded;

        public bool IsExpanded
        {
            set 
            {
                if (_isExpanded == value) return;

                _isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
            }
        }

        #region PropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;
        /// <summary>
        /// This is required to create on property changed events
        /// </summary>
        /// <param name="name">What property of this object has changed</param>
        protected void OnPropertyChanged(string name)
        {
            var handler = PropertyChanged;
            handler?.Invoke(this, new PropertyChangedEventArgs(name));
            if (Validators.ContainsKey(name))
                UpdateError();
        }

        #endregion

        #region Data Validation

        private Dictionary<string, object?> PropertyGetters
        {
            get
            {
                return GetType().GetProperties().Where(p => GetValidations(p).Length != 0).ToDictionary(p => p.Name, GetValueGetter);
            }
        }

        private Dictionary<string, ValidationAttribute[]> Validators
        {
            get
            {
                return GetType().GetProperties().Where(p => GetValidations(p).Length != 0).ToDictionary(p => p.Name, GetValidations);
            }
        }

        private ValidationAttribute[] GetValidations(ICustomAttributeProvider property)
        {
            return (ValidationAttribute[])property.GetCustomAttributes(typeof(ValidationAttribute), true);
        }

        private object? GetValueGetter(PropertyInfo property)
        {
            return property.GetValue(this, null);
        }

        public string Error { get; private set; } = string.Empty;

        private void UpdateError()
        {
            var errors = from i in Validators
                         from v in i.Value
                         where !Validate(v, PropertyGetters[i.Key])
                         select v.ErrorMessage;
            Error = string.Join(Environment.NewLine, errors.ToArray());
            OnPropertyChanged(nameof(Error));
        }

        public string this[string columnName]
        {
            get
            {
                if (PropertyGetters.ContainsKey(columnName))
                {
                    var value = PropertyGetters[columnName];
                    if (value == null) return string.Empty;
                    var errors = Validators[columnName].Where(v => !Validate(v, value))
                        .Select(v => v.ErrorMessage).ToArray();
                    OnPropertyChanged(nameof(Error));
                    return string.Join(Environment.NewLine, errors);
                }

                OnPropertyChanged(nameof(Error));
                return string.Empty;
            }
        }

        private bool Validate(ValidationAttribute v, object value)
        {
            return ValidationContext != null && v.GetValidationResult(value, new ValidationContext(ValidationContext, null, null)) == ValidationResult.Success;
        }

        #endregion
    }
}
