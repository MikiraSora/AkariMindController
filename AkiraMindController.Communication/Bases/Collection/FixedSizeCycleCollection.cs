﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AkiraMindController.Communication.Bases.Collection
{
    public class FixedSizeCycleCollection<T> : IEnumerable<T>
    {
        private T[] arr;
        private int idx = 0;

        public FixedSizeCycleCollection(int size)
        {
            arr = new T[size];
        }

        public IEnumerator<T> GetEnumerator() => arr.AsEnumerable().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Enqueue(T val)
        {
            arr[idx] = val;
            idx = (idx + 1) % arr.Length;
        }

        public void Clear()
        {
            for (int i = 0; i < arr.Length; i++)
                arr[i] = default;
        }
    }
}
