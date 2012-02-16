using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FubuCore.Descriptions;
using FubuCore.Util;
using FubuCore.Util.TextWriting;

namespace FubuCore.Conversion
{
    /// <summary>
    /// Acts as an improved version of the System.ComponentModel.TypeDescriptor class
    /// to store and access strategies for converting a string into a certain Type
    /// </summary>
    public class ConverterLibrary
    {
        private readonly IList<IObjectConverterFamily> _families = new List<IObjectConverterFamily>();
        private readonly Cache<Type, IConverterStrategy> _froms;

        public ConverterLibrary() : this(new IObjectConverterFamily[0])
        {
        }

        public ConverterLibrary(IEnumerable<IObjectConverterFamily> families)
        {
            _froms = new Cache<Type, IConverterStrategy>(createFinder);

            // Strategies that are injected *must* be put first
            _families.AddRange(families);

            _families.Add(new StringConverterStrategy());
            _families.Add(new DateTimeConverter());
            _families.Add(new TimeSpanConverter());
            _families.Add(new TimeZoneConverter());

            _families.Add(new EnumConverterFamily());
            _families.Add(new ArrayConverterFamily());
            _families.Add(new NullableConverterFamily());
            _families.Add(new StringConstructorConverterFamily());
            _families.Add(new TypeDescripterConverterFamily());
        }
// TODO -- may need to do a seal() kind of thing that throws an exception after you call this.
        // or clear the caches, one of the two
        /// <summary>
        /// Register a conversion strategy for a single type by a Func
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="finder"></param>
        public void RegisterConverter<T>(Func<string, T> finder, string description = null)
        {
            _froms[typeof (T)] = new LambdaConverterStrategy<T>(finder, description);
        }


        /// <summary>
        /// Register a conversion strategy for a single type TReturnType that uses
        /// an instance of a service type TService
        /// </summary>
        /// <typeparam name="TReturnType"></typeparam>
        /// <typeparam name="TService"></typeparam>
        /// <param name="converter"></param>
        public void RegisterConverter<TReturnType, TService>(Func<TService, string, TReturnType> converter, string description = null)
        {
            _froms[typeof (TReturnType)] = new LambdaConverterStrategy<TReturnType, TService>(converter, description);
        }


        public void RegisterConverterFamily(IObjectConverterFamily family)
        {
            _families.Insert(0, family);
        }

        private IConverterStrategy createFinder(Type type)
        {
            var family = _families.FirstOrDefault(x => x.Matches(type, this));
            if (family != null)
            {
                return family.CreateConverter(type, t => _froms[t]);
            }

            throw new ArgumentException("No conversion exists for " + type.AssemblyQualifiedName);
        }

        /// <summary>
        /// Can the ConverterLibrary determine a strategy for parsing a string into the Type?
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public bool CanBeParsed(Type type)
        {
            return _froms.Has(type) || _families.Any(x => x.Matches(type, this));
        }

        /// <summary>
        /// Locates or resolves a strategy for converting a string into the requested Type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public IConverterStrategy StrategyFor(Type type)
        {
            return _froms[type];
        }

        public string WhatDoIHave()
        {
            var writer = new StringWriter();

            var familyReport = new TextReportWriter(2);
            familyReport.AddDivider('=');
            familyReport.AddContent("All converter families");
            familyReport.AddDivider('=');
            var i = 1;
            _families.Select(x => Description.GetDescription(x)).Each(desc =>
            {
                familyReport.AddText(i.ToString().PadLeft(3) + ".) " + desc.Title, desc.ShortDescription);
                i++;
            });
            familyReport.Write(writer);
            familyReport.AddDivider('=');

            familyReport.Write(writer);
            writer.WriteLine();


            var conversionReport = new TextReportWriter(3);
            conversionReport.AddDivider('=');
            conversionReport.AddContent("All converter strategies encountered");
            conversionReport.AddDivider('=');

            if (_froms.Count == 0)
            {
                writer.WriteLine("None.");
            }

            Action<Type, IConverterStrategy> addDescription = (type, strategy) =>
            {
                var desc = Description.GetDescription(strategy);
                conversionReport.AddText(type.Name, desc.Title, desc.ShortDescription);
            };

            _froms.Each(addDescription);


            conversionReport.Write(writer);

            return writer.ToString();
        }
    }
}