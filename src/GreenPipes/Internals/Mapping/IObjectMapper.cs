﻿namespace GreenPipes.Internals.Mapping
{
    public interface IObjectMapper<in T>
    {
        void ApplyTo(T obj, IObjectValueProvider valueProvider);
    }
}
