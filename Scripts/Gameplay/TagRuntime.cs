using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using UnityEngine;

namespace Runtime.Game
{
	[Serializable]
	[DebuggerDisplay( "{_type} : {_value.value}" )]
	public class TagRuntime
	{
		public event Action updated;

		internal enum TagStatus
		{
			Present,
			NotPresent,
			Voided,
		}

		[ReadOnly] internal bool _dirty = true;
		protected ReadWriteProperty<bool> _value = new();

		IComplexStatsRuntime _owner;
		[SerializeField] private Schema.TagType _type;
		private TagRuntime _parent;

		public Schema.TagType type { get => _type; }

		private List<Property<bool>> _baseValues = new();

		[SerializeField] private TagStatus _baseValue;
		public bool baseValue { get => _baseValue == TagStatus.Present; }

		public TagRuntime( IComplexStatsRuntime owner, Schema.TagType type )
		{
			_owner = owner;
			_type = type;
		}

		public void SetParent( TagRuntime parent )
		{
			if ( parent == _parent )
				return;
			if ( parent == this )
			{
				UnityEngine.Debug.LogError( "Trying to set a tag as its own parent : " + type.ToString() );
				return;
			}
			if ( _parent != null )
				_parent.updated -= Dirty;
			_parent = parent;
			if ( _parent != null )
				_parent.updated += Dirty;
			Dirty();
		}

		public Property<bool> GetValue()
		{
			if ( _dirty )
				ComputeValue();
			return _value;
		}

		protected void Dirty()
		{
			_dirty = true;
			updated?.Invoke();
		}

		public void Add( Property<bool> property )
		{
			if ( !_baseValues.Contains( property ) )
			{
				_baseValues.Add( property );
				property.Bind( ValueChanged );
			}
		}

		public void Remove( Property<bool> property )
		{
			if ( _baseValues.Remove( property ) )
			{
				property.Unbind( ValueChanged );
				Dirty();
			}
		}

		internal void ComputeValue()
		{
			if ( _parent != null && _parent._dirty )
				_parent.ComputeValue();
			bool tmpValue = GetCompoundedBaseValue() == TagStatus.Present;
			_dirty = false;
			_value.SetValue( tmpValue );
		}

		private void ValueChanged( bool value )
		{
			Dirty();
		}

		internal TagStatus GetCompoundedBaseValue()
		{
			if ( _dirty )
			{
				_baseValue = _baseValues.Count == 0 ? TagStatus.NotPresent : TagStatus.Present;
				foreach ( var baseValue in _baseValues )
				{
					if ( !baseValue.value )
					{
						_baseValue = TagStatus.Voided;
						return _baseValue;
					}
				}
				var parentBaseValue = _parent != null ? _parent.GetCompoundedBaseValue() : TagStatus.NotPresent;
				if ( parentBaseValue != TagStatus.NotPresent )
					_baseValue = parentBaseValue;
			}
			return _baseValue;
		}
	}
}
