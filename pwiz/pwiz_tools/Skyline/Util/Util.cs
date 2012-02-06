﻿/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.SettingsUI;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Implement on an element for use with <see cref="MappedList{TKey,TValue}"/>.
    /// </summary>
    /// <typeparam name="TKey">Key type in the map</typeparam>
    public interface IKeyContainer<TKey>
    {
        TKey GetKey();
    }

    /// <summary>
    /// Base class for use with elements to be stored in
    /// <see cref="MappedList{TKey,TValue}"/>.
    /// </summary>
    public abstract class NamedElement : IKeyContainer<string>
    {
        protected NamedElement(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }

        public virtual string GetKey()
        {
            return Name;
        }

        #region object overrides

        public bool Equals(NamedElement obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj.Name, Name);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(NamedElement)) return false;
            return Equals((NamedElement)obj);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        #endregion
    }

    /// <summary>
    /// Allows access to a default state for a collection that allows
    /// editing.
    /// </summary>
    /// <typeparam name="TItem">The type of the items in the collection</typeparam>
    public interface IListDefaults<TItem>
    {
        /// <summary>
        /// Gets the current revision index for this list
        /// </summary>
        int RevisionIndexCurrent { get; }

        /// <summary>
        /// Gets the default collection as an enumerable list.
        /// </summary>
        /// <returns>The default collection</returns>
        IEnumerable<TItem> GetDefaults(int revisionIndex);
    }

    /// <summary>
    /// Exposes properties necessary for using <see cref="EditListDlg{T,TItem}"/>
    /// to edit a list.
    /// </summary>
    public interface IListEditorSupport
    {
        /// <summary>
        /// The title to display on <see cref="EditListDlg{T,TItem}"/>
        /// </summary>
        string Title { get; }

        /// <summary>
        /// Label string for the listbox that shows this list being
        /// edited.
        /// </summary>
        string Label { get; }

        /// <summary>
        /// True if the list can be reset to its default contents.
        /// </summary>
        bool AllowReset { get; }

        /// <summary>
        /// True if default item should be exclude when editing
        /// the list.  Useful when the default item cannot be edited
        /// or removed from the list.
        /// </summary>
        bool ExcludeDefaults { get; }
    }

    /// <summary>
    /// Implement this interfact to support the <see cref="EditListDlg{T,TItem}"/>.
    /// </summary>
    /// <typeparam name="TItem">Type of items in the list to be edited</typeparam>
    public interface IListEditor<TItem>
    {
        /// <summary>
        /// Exposes ability to edit a list of items.
        /// </summary>
        /// <returns>The new list after editing, or null if the user cancelled</returns>
        IEnumerable<TItem> EditList(Control owner, object tag);

        /// <summary>
        /// Returns true, if a new list is accepted to replace the current list
        /// </summary>
        bool AcceptList(Control owner, IList<TItem> listNew);
    }

    /// <summary>
    /// Implement this interfact to support the <see cref="ShareListDlg{T,TItem}"/>.
    /// </summary>
    /// <typeparam name="TItem">Type of items in the list to be edited</typeparam>
    public interface IListSerializer<TItem>
    {
        Type SerialType { get; }

        ICollection<TItem> CreateEmptyList();

        bool ContainsKey(string key);
    }

    /// <summary>
    /// Implement this interface to support the "Add" and "Edit"
    /// buttons in the <see cref="EditListDlg{T,TItem}"/>.
    /// </summary>
    /// <typeparam name="TItem">Type of items in the list to be edited</typeparam>
    public interface IItemEditor<TItem>
    {
        /// <summary>
        /// Exposes the ability to create a new item for this list.
        /// </summary>
        /// <param name="owner">Window requesting the edit</param>
        /// <param name="existing">A list of existing items of this type</param>
        /// <param name="tag">Object passed to the list editor for use in item editors</param>
        /// <returns>The new item, or null if the user cancelled</returns>
        TItem NewItem(Control owner, IEnumerable<TItem> existing, object tag);

        /// <summary>
        /// Exposes the ability to edit an individual item, return
        /// a new modified item.  Items are considered immutable,
        /// so successful return value will always be a new item.
        /// </summary>
        /// <param name="owner">Window requesting the edit</param>
        /// <param name="item">The item to edit</param>
        /// <param name="existing">A list of existing items of this type</param>
        /// <param name="tag">Object passed to the list editor for use in item editors</param>
        /// <returns>The new item, or null if the user cancelled</returns>
        TItem EditItem(Control owner, TItem item, IEnumerable<TItem> existing, object tag);

        /// <summary>
        /// Copies an item for this list, with the copied item's name reset
        /// to the empty string.
        /// </summary>
        /// <param name="item">The item to copy</param>
        /// <returns>The copied item with empty name</returns>
        TItem CopyItem(TItem item);
    }

    /// <summary>
    /// A generic ordered list based on Collection&lt;TValue>, with
    /// elements also stored in a private dictionary for fast lookup.
    /// Sort of a substitute for LinkedHashMap in Java.
    /// </summary>
    /// <typeparam name="TKey">Type of the key used in the map</typeparam>
    /// <typeparam name="TValue">Type stored in the collection</typeparam>
    public class MappedList<TKey, TValue>
        : Collection<TValue>
        where TValue : IKeyContainer<TKey>
    {
        private readonly Dictionary<TKey, TValue> _dict = new Dictionary<TKey, TValue>();

        public TValue this[TKey name]
        {
            get
            {
                return _dict[name];
            }
        }

        public IEnumerable<TKey> Keys
        {
            get
            {
                foreach (TValue value in this)
                    yield return value.GetKey();
            }
        }

        public bool ContainsKey(TKey key)
        {
            return _dict.ContainsKey(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _dict.TryGetValue(key, out value);
        }

        public void SetValue(TValue value)
        {
            TValue valueCurrent;
            if (TryGetValue(value.GetKey(), out valueCurrent))
            {
                SetItem(IndexOf(valueCurrent), value);
            }
            else
            {
                Add(value);
            }
        }

        public void AddRange(IEnumerable<TValue> collection)
        {
            foreach (TValue item in collection)
                Add(item);
        }

        #region INamer<TValue> Members

        public TKey GetKey(TValue item)
        {
            return item.GetKey();
        }

        #endregion

        #region Collection<TValue> Overrides

        protected override void ClearItems()
        {
            _dict.Clear();
            base.ClearItems();
        }

        protected override void InsertItem(int index, TValue item)
        {
            int i = RemoveExisting(item);
            if (i != -1 && i < index)
                index--;
            _dict.Add(item.GetKey(), item);
            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            _dict.Remove(this[index].GetKey());
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, TValue item)
        {
            TKey key = this[index].GetKey();

            // If setting to a list item that has a different key
            // from what is at this location currently, then any
            // existing value with the same key must be removed
            // from its current location.
            if (!Equals(key, item.GetKey()))
            {
                int i = RemoveExisting(item);
                if (i != -1 && i < index)
                    index--;

                // If the index pointed at an item with a different
                // key, then removing some other item cannot leave
                // the index out of range.
                Debug.Assert(index < Items.Count);
            }
            _dict.Remove(key);
            _dict.Add(item.GetKey(), item);
            base.SetItem(index, item);                
        }

        /// <summary>
        /// Used to help ensure that only one copy of the keyed elements
        /// can exist in the list at any time.
        /// </summary>
        /// <param name="item">An item to remove</param>
        /// <returns>The index from which it was removed, or -1 if not found</returns>
        private int RemoveExisting(TValue item)
        {
            TKey key = item.GetKey();
            if (_dict.ContainsKey(key))
            {
                _dict.Remove(key);
                for (int i = 0; i < Items.Count; i++)
                {
                    if (Equals(Items[i].GetKey(), item.GetKey()))
                    {
                        RemoveAt(i);
                        return i;
                    }
                }
            }
            return -1;
        }

        #endregion // Collection<TValue> Overrides
    }

    public class MultiMap<TKey, TValue>
    {
        readonly Dictionary<TKey, List<TValue>> _dict;

        public MultiMap()            
        {
            _dict = new Dictionary<TKey, List<TValue>>();
        }

        public MultiMap(int capacity)
        {
            _dict = new Dictionary<TKey, List<TValue>>(capacity);
        }

        public void Add(TKey key, TValue value)
        {
            List<TValue> values;
            if (_dict.TryGetValue(key, out values))
                values.Add(value);
            else
                _dict[key] = new List<TValue> { value };
        }

        public IEnumerable<TKey> Keys { get { return _dict.Keys; } }

        public IList<TValue> this[TKey key] { get { return _dict[key]; } }

        public bool TryGetValue(TKey key, out IList<TValue> values)
        {
            List<TValue> listValues;
            if (_dict.TryGetValue(key, out listValues))
            {
                values = listValues;
                return true;
            }
            values = null;
            return false;
        }
    }

    public static class MapUtil
    {
        public static MultiMap<TKey, TValue> ToMultiMap<TKey, TValue>(this IEnumerable<TValue> values, Func<TValue, TKey> keySelector)
        {
            MultiMap<TKey, TValue> map = new MultiMap<TKey, TValue>();
            foreach (TValue value in values)
                map.Add(keySelector(value), value);
            return map;
        }
    }

    /// <summary>
    /// A read-only list class for the case when a list most commonly contains a
    /// single entry, but must also support multiple entries.  This list may not
    /// be empty, thought it may contain a single null element.
    /// </summary>
    /// <typeparam name="TItem">Type of the elements in the list</typeparam>
    public class OneOrManyList<TItem> : IList<TItem>
//        VS Issue: https://connect.microsoft.com/VisualStudio/feedback/ViewFeedback.aspx?FeedbackID=324473
//        where T : class
    {
        private TItem _one;
        private TItem[] _many;

        public OneOrManyList(params TItem[] elements)
        {
            if (elements.Length > 1)
                _many = elements;
            else if (elements.Length == 1)
                _one = elements[0];            
        }

        public OneOrManyList(IList<TItem> elements)
        {
            if (elements.Count > 1)
                _many = elements.ToArray();
            else if (elements.Count == 1)
                _one = elements[0];
        }

        public IEnumerator<TItem> GetEnumerator()
        {
            return GetEnumerable().GetEnumerator();
        }

        private IEnumerable<TItem> GetEnumerable()
        {
            if (Equals(_many, null))
            {
                yield return _one;
            }
            else
            {
                foreach (var item in _many)
                    yield return item;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(TItem item)
        {
            throw new ReadOnlyException("Attempted modification of a read-only collection.");
        }

        public void Clear()
        {
            throw new ReadOnlyException("Attempted modification of a read-only collection.");
        }

        public bool Contains(TItem item)
        {
            if (Equals(_many, null))
                return Equals(_one, item);
            return _many.Contains(item);
        }

        public void CopyTo(TItem[] array, int arrayIndex)
        {
            if (Equals(_many, null))
                array[arrayIndex] = _one;
            else
                _many.CopyTo(array, arrayIndex);            
        }

        public bool Remove(TItem item)
        {
            throw new ReadOnlyException("Attempted modification of a read-only collection.");
        }

        public int Count
        {
            get
            {
                if (Equals(_many, null))
                    return 1;
                return _many.Length;
            }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public int IndexOf(TItem item)
        {
            if (Equals(_many, null))
                return Equals(_one, item) ? 0 : -1;
            return _many.IndexOf(v => Equals(v, item));
        }

        public void Insert(int index, TItem item)
        {
            throw new ReadOnlyException("Attempted modification of a read-only collection.");
        }

        public void RemoveAt(int index)
        {
            throw new ReadOnlyException("Attempted modification of a read-only collection.");
        }

        public TItem this[int index]
        {
            get
            {
                ValidateIndex(index);
                if (Equals(_many, null))
                    return _one;
                return _many[index];
            }

            set
            {
                throw new ReadOnlyException("Attempted modification of a read-only collection.");
            }
        }

        public OneOrManyList<TItem> ChangeAt(int index, TItem item)
        {
            ValidateIndex(index);
            var cloneNew = (OneOrManyList<TItem>) MemberwiseClone();
            if (Equals(_many, null))
                cloneNew._one = item;
            else
            {
                cloneNew._many = new TItem[_many.Length];
                Array.Copy(_many, cloneNew._many, _many.Length);
                cloneNew._many[index] = item;
            }
            return cloneNew;
        }

        private void ValidateIndex(int index)
        {
            if (Equals(_many, null))
            {
                if (index != 0)
                    throw new IndexOutOfRangeException(string.Format("The index {0} must be 0 for a single entry list.", index));
            }
            else if (0 > index || index > _many.Length)
                throw new IndexOutOfRangeException(string.Format("The index {0} must be between 0 and {1}.", index, _many.Length));
        }

        #region object overrides

        public bool Equals(OneOrManyList<TItem> obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj._one, _one) &&
                ArrayUtil.EqualsDeep(obj._many, _many);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((OneOrManyList<TItem>) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
// ReSharper disable NonReadonlyFieldInGetHashCode
                return ((!Equals(_one, default(TItem)) ? _one.GetHashCode() : 0)*397) ^
                    (_many != null ? _many.GetHashCodeDeep() : 0);
// ReSharper restore NonReadonlyFieldInGetHashCode
            }
        }

        #endregion
    }
    
    /// <summary>
    /// Exposes a set of generic Array extension utility functions.
    /// </summary>
    public static class ArrayUtil
    {
        /// <summary>
        /// Returns the length of an array or zero if it is null.
        /// </summary>
        public static int SafeLength<TItem>(this IList<TItem> values)
        {
            return values != null ? values.Count : 0;
        }

        /// <summary>
        /// Parses an array of items from a string, which are separated by
        /// a specific character (e.g. "1, 2, 3" or "3.5; 4.5; 5.5").  Whitespace
        /// is trimmed.
        /// </summary>
        /// <typeparam name="TItem">Type of items in the array returned</typeparam>
        /// <param name="values">The string to parse</param>
        /// <param name="conv">An instance of a string to T converter</param>
        /// <param name="separatorChar">The separator character</param>
        /// <param name="defaults">A default array to return, if the string is null or empty</param>
        /// <returns></returns>
        public static TItem[] Parse<TItem>(string values, Converter<string, TItem> conv,
            char separatorChar, params TItem[] defaults)
        {
            if (!string.IsNullOrEmpty(values))
            {
                try
                {
                    List<TItem> list = new List<TItem>();
                    string[] parts = values.Split(separatorChar);
                    foreach (string part in parts)
                        list.Add(conv(part.Trim()));
                    return list.ToArray();
                }
// ReSharper disable EmptyGeneralCatchClause
                catch (Exception)
// ReSharper restore EmptyGeneralCatchClause
                {
                }
            }
            return defaults;
        }

        /// <summary>
        /// Joins the ToString() value for an array of objects, with a specified
        /// separator character between each item.
        /// </summary>
        /// <typeparam name="TItem">The type of the items in the array</typeparam>
        /// <param name="values">The array of items to join</param>
        /// <param name="separator">The separator character to place between strings</param>
        /// <returns>A joined string of items with intervening separators</returns>
        public static string ToString<TItem>(this IList<TItem> values, string separator)
        {
            StringBuilder sb = new StringBuilder();
            foreach (TItem value in values)
            {
                if (sb.Length > 0)
                    sb.Append(separator);
                sb.Append(value);
            }
            return sb.ToString();
        }

        public static TItem[] ToArrayStd<TItem>(this IList<TItem> list)
        {
            if (list is TItem[])
                return (TItem[]) list;

            TItem[] a = new TItem[list.Count];
            for (int i = 0; i < a.Length; i++)
                a[i] = list[i];
            return a;
        }

        /// <summary>
        /// Gets a <see cref="IEnumerable{T}"/> for enumerating over an Array.
        /// </summary>
        /// <typeparam name="TItem">Type of items in the array</typeparam>
        /// <param name="values">Array instance</param>
        /// <param name="forward">True if the enumerator should be forward, False if reversed</param>
        /// <returns>The enumeration of the Array</returns>
        public static IEnumerable<TItem> GetEnumerator<TItem>(this IList<TItem> values, bool forward)
        {
            if (forward)
            {
                foreach (TItem value in values)
                    yield return value;
            }
            else
            {
                for (int i = values.Count - 1; i >= 0; i--)
                    yield return values[i];
            }
        }

        /// <summary>
        /// Creates a random order of indexes into an array for a random linear walk
        /// through an array.
        /// </summary>
        public static IEnumerable<TItem> RandomOrder<TItem>(this IList<TItem> list)
        {
            int count = list.Count;
            var indexOrder = new int[count];
            for (int i = 0; i < count; i++)
                indexOrder[i] = i;
            Random r = new Random(DateTime.Now.Millisecond);    // TODO: fix
            for (int i = 0; i < count; i++)
                Helpers.Swap(ref indexOrder[0], ref indexOrder[r.Next(count - 1)]);
            foreach (int i in indexOrder)
            {
                yield return list[i];
            }
        }

        /// <summary>
        /// Searches an Array for an item that is reference equal with
        /// a specified item to find.
        /// </summary>
        /// <typeparam name="TItem">Type of item in the array</typeparam>
        /// <param name="values">The Array to search</param>
        /// <param name="find">The item to find</param>
        /// <returns>The index in the Array of the specified reference, or -1 if not found</returns>
        public static int IndexOfReference<TItem>(this IList<TItem> values, TItem find)
        {
            return values.IndexOf(value => ReferenceEquals(value, find));
        }

        /// <summary>
        /// Searches an Array for an item that matches criteria specified
        /// through a delegate function.
        /// </summary>
        /// <typeparam name="TItem">Type of item in the array</typeparam>
        /// <param name="values">The Array to search</param>
        /// <param name="found">Delegate accepting an item, and returning true if it matches</param>
        /// <returns>The index in the Array of the match, or -1 if not found</returns>
        public static int IndexOf<TItem>(this IList<TItem> values, Predicate<TItem> found)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (found(values[i]))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Searches backward in an Array for an item that matches criteria specified
        /// through a delegate function.
        /// </summary>
        /// <typeparam name="TItem">Type of item in the array</typeparam>
        /// <param name="values">The Array to search</param>
        /// <param name="found">Delegate accepting an item, and returning true if it matches</param>
        /// <returns>The index in the Array of the last match, or -1 if not found</returns>
        public static int LastIndexOf<TItem>(this IList<TItem> values, Predicate<TItem> found)
        {
            for (int i = values.Count -1; i >= 0; i--)
            {
                if (found(values[i]))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Searches an Array for an item that matches criteria specified
        /// through a delegate function.
        /// </summary>
        /// <typeparam name="TItem">Type of item in the array</typeparam>
        /// <param name="values">The Array to search</param>
        /// <param name="found">Delegate accepting an item, and returning true if it matches</param>
        /// <returns>True if the accepting function returns true for an element</returns>
        public static bool Contains<TItem>(this IEnumerable<TItem> values, Predicate<TItem> found)
        {
            foreach (TItem value in values)
            {
                if (found(value))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Checks for deep equality, or equality of all items in an Array.
        /// </summary>
        /// <typeparam name="TItem">Type of items in the array</typeparam>
        /// <param name="values1">First array in the comparison</param>
        /// <param name="values2">Second array in the comparison</param>
        /// <returns>True if all items in both arrays in identical positions are Equal</returns>
        public static bool EqualsDeep<TItem>(IList<TItem> values1, IList<TItem> values2)
        {
            if (values1 == null && values2 == null)
                return true;
            if (values1 == null || values2 == null)
                return false;
            if (values1.Count != values2.Count)
                return false;
            for (int i = 0; i < values1.Count; i++)
            {
                if (!Equals(values1[i], values2[i]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Constructs a hash-code for an Array from all of the items in
        /// the array.
        /// </summary>
        /// <typeparam name="TItem">Type of the items in the array</typeparam>
        /// <param name="values">The Array instance</param>
        /// <returns>A hash-code value constructed from all items in the array</returns>
        public static int GetHashCodeDeep<TItem>(this IList<TItem> values)
        {
            return values.GetHashCodeDeep(v => v.GetHashCode());
        }

        public static int GetHashCodeDeep<TItem>(this IList<TItem> values, Func<TItem, int> getHashCode)
        {
            unchecked
            {
                int result = 0;
                foreach (TItem value in values)
                    result = (result * 397) ^ (!Equals(value, default(TItem)) ? getHashCode(value) : 0);
                return result;
            }
        }

        /// <summary>
        /// Checks if all elements in one list are <see cref="object.ReferenceEquals"/>
        /// with the elements in another list.
        /// </summary>
        /// <typeparam name="TItem">Type of the list elements</typeparam>
        /// <param name="values1">The first list in the comparison</param>
        /// <param name="values2">The second list in the comparison</param>
        /// <returns>True if all references in the lists are equal to each other</returns>
        public static bool ReferencesEqual<TItem>(IList<TItem> values1, IList<TItem> values2)
        {
            if (values1 == null && values2 == null)
                return true;
            if (values1 == null || values2 == null)
                return false;
            if (values1.Count != values2.Count)
                return false;
            for (int i = 0; i < values1.Count; i++)
            {
                if (!ReferenceEquals(values1[i], values2[i]))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Enumerates two lists assigning references from the second list to
        /// entries in the first list, where they are equal.  Useful for maintaining
        /// reference equality when recalculating values. Similar to <see cref="Helpers.AssignIfEquals{T}"/>.
        /// </summary>
        /// <typeparam name="TItem">Type of the list elements</typeparam>
        /// <param name="values1">The first list in the comparison</param>
        /// <param name="values2">The second list in the comparison</param>
        public static void AssignIfEqualsDeep<TItem>(IList<TItem> values1, IList<TItem> values2)
        {
            if (values1 == null || values2 == null)
                return;
            for (int i = 0, len = Math.Min(values1.Count, values2.Count); i < len; i++)
            {
                if (Equals(values1[i], values2[i]))
                    values1[i] = values2[i];
            }
        }
    }

    /// <summary>
    /// A set of generic, static helper functions.
    /// </summary>
    public static class Helpers
    {
        /// <summary>
        /// Swaps two reference values in memory, making each contain
        /// the reference the other started with.
        /// </summary>
        /// <typeparam name="TItem">Type of the two values</typeparam>
        /// <param name="val1">Left value</param>
        /// <param name="val2">Right value</param>
        public static void Swap<TItem>(ref TItem val1, ref TItem val2)
        {
            TItem tmp = val1;
            val1 = val2;
            val2 = tmp;
        }

        /// <summary>
        /// Assigns the a source reference to the intended destination,
        /// only if they are <see cref="object.Equals(object,object)"/>.
        /// 
        /// This can be useful in combination with immutable objects,
        /// allowing the caller choose an existing object already referenced
        /// in a data structure over a newly created instance, if the two
        /// are identical in value.
        /// </summary>
        /// <typeparam name="TItem"></typeparam>
        /// <param name="dest"></param>
        /// <param name="src"></param>
        public static void AssignIfEquals<TItem>(ref TItem dest, TItem src)
        {
            if (Equals(dest, src))
                dest = src;
        }

        /// <summary>
        /// Compare two IEnumerable instances for equality.
        /// </summary>
        /// <typeparam name="TItem">The type of element being enumerated</typeparam>
        /// <param name="e1">The first IEnumerable</param>
        /// <param name="e2">The second IEnumberable</param>
        /// <returns>True if the two IEnumerables enumerate over equal objects</returns>
        public static bool Equals<TItem>(IEnumerable<TItem> e1, IEnumerable<TItem> e2)
        {
            IEnumerator<TItem> enum1 = e1.GetEnumerator();
            IEnumerator<TItem> enum2 = e2.GetEnumerator();
            bool b1, b2;
            while (MoveNext(enum1, out b1, enum2, out b2))
            {
                if (!Equals(enum1.Current, enum2.Current))
                    break;
            }

            // If both enums have advanced to completion without finding
            // a difference, then they are equal.
            return (!b1 && !b2);
        }

        /// <summary>
        /// Call MoveNext on two IEnumerator instances in one operation,
        /// but avoid short-circuiting of (e1.MoveNext() && e2.MoveNext),
        /// and pass the return values of both as out parameters.
        /// </summary>
        /// <param name="e1">First Enumerator to advance</param>
        /// <param name="b1">Return value of e1.MoveNext()</param>
        /// <param name="e2">Second Enumerator to advance</param>
        /// <param name="b2">Return value of e2.MoveNext()</param>
        /// <returns>True if both calls to MoveNext() succeed</returns>
        private static bool MoveNext(IEnumerator e1, out bool b1,
            IEnumerator e2, out bool b2)
        {
            b1 = e1.MoveNext();
            b2 = e2.MoveNext();
            return b1 && b2;
        }

        /// <summary>
        /// Parses an enum value from a string, returning a default value,
        /// if the string fails to parse.
        /// </summary>
        /// <typeparam name="TEnum">The enum type</typeparam>
        /// <param name="value">The string to parse</param>
        /// <param name="defaultValue">The value to return, if parsing fails</param>
        /// <returns>An enum value of type <see cref="TEnum"/></returns>
        public static TEnum ParseEnum<TEnum>(string value, TEnum defaultValue)
        {
            try
            {
                return (TEnum)Enum.Parse(typeof(TEnum), value);
            }
            catch (Exception)
            {
                return defaultValue;
            }                            
        }

        public static string MakeId(IEnumerable<char> name)
        {
            return MakeId(name, false);
        }

        public static string MakeId(IEnumerable<char> name, bool capitalize)
        {
            StringBuilder sb = new StringBuilder();
            char lastC = '\0';
            foreach (var c in name)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (lastC == ' ')
                        sb.Append('_');
                    lastC = c;
                    if (capitalize && sb.Length == 0)
                        sb.Append(c.ToString(CultureInfo.InvariantCulture).ToUpper());
                    else
                        sb.Append(c);
                }
                // Must start with a letter or digit
                else if (lastC != '\0')
                {
                    // After the start _ okay (dashes turned out to be problematic)
                    if (c == '_' /* || c == '-'*/)
                        sb.Append(lastC = c);
                    // All other characters are replaced with _, but once the next
                    // letter or number is seen.
                    else if (char.IsLetterOrDigit(lastC))
                        lastC = ' ';
                }
            }
            return sb.ToString();
        }

        private static readonly Regex REGEX_XML_ID = new Regex("/^[:_A-Za-z][-.:_A-Za-z0-9]*$/");
        private const string XML_ID_FIRST_CHARS = ":_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        private const string XML_ID_FOLLOW_CHARS = "-.:_ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        private const string XML_NON_ID_SEPARATOR_CHARS = ";[]{}()!|\\/\"'<>";
        private const string XML_NON_ID_PUNCTUATION_CHARS = ",?";

        public static string MakeXmlId(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new InvalidOperationException("Failure creating XML ID. Input string may not be empty.");
            if (REGEX_XML_ID.IsMatch(name))
                return name;

            var sb = new StringBuilder();
            int i = 0;
            if (XML_ID_FIRST_CHARS.Contains(name[i]))
                sb.Append(name[i++]);
            else
            {
                sb.Append('_');
                // If the first character is not allowable, advance past it.
                // Otherwise, keep it in the ID.
                if (!XML_ID_FOLLOW_CHARS.Contains(name[i]))
                    i++;
            }
            for (; i < name.Length; i++)
            {
                char c = name[i];
                if (XML_ID_FOLLOW_CHARS.Contains(c))
                    sb.Append(c);
                else if (char.IsWhiteSpace(c))
                    sb.Append('_');
                else if (XML_NON_ID_SEPARATOR_CHARS.Contains(c))
                    sb.Append(':');
                else if (XML_NON_ID_PUNCTUATION_CHARS.Contains(c))
                    sb.Append('.');
                else
                    sb.Append('-');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Given a proposed name and a set of existing names, returns a unique name by adding
        /// or incrementing an integer suffix.
        /// </summary>
        /// <param name="name">A proposed name to add</param>
        /// <param name="set">A set of existing names</param>
        /// <returns>A new unique name that can be safely added to the existing set without name conflict</returns>
        public static string GetUniqueName(string name, ICollection<string> set)
        {
            if (!set.Contains(name))
                return name;

            int num = 1;
            // If the name has an integer suffix, start searching with the base name
            // and the integer suffix incremented by 1.
            int i = GetIntSuffixStart(name);
            if (i < name.Length)
            {
                num = int.Parse(name.Substring(i)) + 1;
                name = name.Substring(0, i);
            }
            // Loop until a unique base name and integer suffix combination is found.
            while (set.Contains(name + num))
                num++;
            return name + num;
        }

        /// <summary>
        /// Given a name returns the start index of an integer suffix, if the name has one,
        /// or the length of the string, if no integer suffix is present.
        /// </summary>
        /// <param name="name">A name to analyze</param>
        /// <returns>The starting position of an integer suffix or the length of the string, if the name does not have one</returns>
        private static int GetIntSuffixStart(string name)
        {
            for (int i = name.Length; i > 0; i--)
            {
                int num;
                if (!int.TryParse(name.Substring(i - 1), out num))
                    return i;
            }
            return 0;
        }

        /// <summary>
        /// Count the number of lines in the file specified.
        /// </summary>
        /// <param name="f">The filename to count lines in.</param>
        /// <returns>The number of lines in the file.</returns>
        public static long CountLinesInFile(string f)
        {
            long count = 0;
            using (StreamReader r = new StreamReader(f))
            {
                while (r.ReadLine() != null)
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Count the number of lines in the string specified.
        /// </summary>
        /// <param name="s">The string to count lines in.</param>
        /// <returns>The number of lines in the string.</returns>
        public static long CountLinesInString(string s)
        {
            long count = 1;
            int start = 0;
            while ((start = s.IndexOf('\n', start)) != -1)
            {
                count++;
                start++;
            }
            return count;
        }

        private const char LABEL_SEP_CHAR = '_';
        private const string ELIPSIS = "...";
        private static readonly char[] SPACE_CHARS = new[] {'_', '-', ' ', '.', ','};

        /// <summary>
        /// Finds repetitive text in labels and removes the text to save space.
        /// </summary>
        /// <param name="labels">The labels we are removing redundant text from.</param>
        /// <param name="startLabelIndex">Index we want to start looking at, in case the Expected/Library
        /// label is showing.</param>
        /// <returns>Return </returns>
        public static bool RemoveRepeatedLabelText(string[] labels, int startLabelIndex)
        {
            // Check to see if there are any labels. 
            if (labels.Length == startLabelIndex)
                return false;

            // Creat a normalized set of labels to test for repeated text
            string[] labelsRemove = new string[labels.Length];

            Array.Copy(labels, labelsRemove, labels.Length);

            if (startLabelIndex != 0)
            {
                labelsRemove = new string[labelsRemove.Length - startLabelIndex];
                Array.Copy(labels, startLabelIndex, labelsRemove, 0, labelsRemove.Length);
            }

            for (int i = 0; i < labelsRemove.Length; i++)
                labelsRemove[i] = NormalizeSeparators(labelsRemove[i]);

            var labelParts = labelsRemove[0].Split(LABEL_SEP_CHAR);

            // If all labels start with the first part
            string replaceString = labelParts[0];
            string partFirst = replaceString + LABEL_SEP_CHAR;
            if (!labelsRemove.Contains(label => !label.StartsWith(partFirst)))
            {
                RemoveString(labels, startLabelIndex, replaceString, ReplaceLocation.start);
                return true;
            }

            // If all labels end with the last part
            replaceString = labelParts[labelParts.Length - 1];
            string partLast = LABEL_SEP_CHAR + replaceString;
            if (!labelsRemove.Contains(label => !label.EndsWith(partLast)))
            {
                RemoveString(labels, startLabelIndex, replaceString, ReplaceLocation.end);
                return true;
            }

            for (int i = 1 ; i < labelParts.Length - 1; i++)
            {
                replaceString = labelParts[i];
                string partMiddle = LABEL_SEP_CHAR + replaceString + LABEL_SEP_CHAR;
                // If all labels contain the middle part
                if (!labelsRemove.Contains(label => !label.Contains(partMiddle)))
                {
                    RemoveString(labels, startLabelIndex, replaceString, ReplaceLocation.middle);
                    return true;
                }
            }

            return false;
        }

        private static bool IsSpaceChar(char c)
        {
            return SPACE_CHARS.Contains(c);
        }

        private static string NormalizeSeparators(string startLabelText)
        {
            startLabelText = startLabelText.Replace(ELIPSIS, LABEL_SEP_CHAR.ToString(CultureInfo.InvariantCulture));
            foreach (var spaceChar in SPACE_CHARS)
            {
                startLabelText = startLabelText.Replace(spaceChar, LABEL_SEP_CHAR);
            }

            return startLabelText;
        }

        /// <summary>
        /// Truncates labels.
        /// </summary>
        /// <param name="labels">Labels text will be removed from.</param>
        /// <param name="startLabelIndex">Index we want to start looking at, in case the Expected/Library
        /// label is showing.</param>
        /// <param name="replaceString">Text being removed from labels.</param>
        /// <param name="location">Expected location of the replacement text</param>
        public static void RemoveString(string[] labels, int startLabelIndex, string replaceString, ReplaceLocation location)
        {
            for (int i = startLabelIndex; i < labels.Length; i++)
                labels[i] = RemoveString(labels[i], replaceString, location);
        }

        public enum ReplaceLocation {start, middle, end}

        private static string RemoveString(string label, string replaceString, ReplaceLocation location)
        {
            int startIndex = -1;
            while ((startIndex = label.IndexOf(replaceString, startIndex + 1, StringComparison.Ordinal)) != -1)
            {
                int endIndex = startIndex + replaceString.Length;
                // Not start string and does not end with space
                if ((startIndex != 0 && !IsSpaceChar(label[startIndex - 1])) || 
                    (startIndex == 0 && location != ReplaceLocation.start))
                    continue;
                
                // Not end string and does not start with space
                if ((endIndex != label.Length && !IsSpaceChar(label[endIndex])) ||
                    (endIndex == label.Length && location != ReplaceLocation.end))
                    continue;
                
                bool elipsisSeen = false;
                bool middle = true;
                // Check left of the string for the start of the label or a space char
                if (startIndex == 0)
                    middle = false;
                else if (startIndex >= ELIPSIS.Length && label.LastIndexOf(ELIPSIS, startIndex, StringComparison.Ordinal) == startIndex - ELIPSIS.Length)
                    elipsisSeen = true;
                else
                    startIndex--;
                
                // Check right of the string for the end of the label or a space char
                if (endIndex == label.Length)
                    middle = false;
                else if (label.IndexOf(ELIPSIS, endIndex, StringComparison.Ordinal) == endIndex)
                    elipsisSeen = true;
                else
                    endIndex++;
                label = label.Remove(startIndex, endIndex - startIndex);
                // Insert an elipsis, if this is in the middle and no elipsis has been seen
                if (middle && !elipsisSeen && location == ReplaceLocation.middle)
                    label = label.Insert(startIndex, ELIPSIS);
                return label;
            }
            return label;
        }
    }

    public static class MathEx
    {
        public static double RoundAboveZero(float value, int startDigits, int mostDigits)
        {
            for (int i = startDigits; i <= mostDigits; i++)
            {
                double rounded = Math.Round(value, i);
                if (rounded > 0)
                    return rounded;
            }
            return 0;
        }        
    }
}