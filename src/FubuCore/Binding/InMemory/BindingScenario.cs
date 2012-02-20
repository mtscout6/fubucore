using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using FubuCore.Reflection;
using FubuCore.Util;

namespace FubuCore.Binding.InMemory
{
    public class BindingScenario<T> where T : class, new()
    {
        private readonly StringWriter _writer = new StringWriter();

        private BindingScenario(ScenarioDefinition definition)
        {
            var context = new BindingContext(definition.RequestData, definition.Services, new NulloBindingLogger());

            context.ForObject(definition.Model, () => definition.Actions.Each(x => x(context)));

            Model = definition.Model;
            Problems = context.Problems;
        }

        public IList<ConvertProblem> Problems { get; private set; }

        public T Model { get; private set; }

        public string Log
        {
            get { return _writer.GetStringBuilder().ToString(); }
        }

        public static BindingScenario<T> For(Action<ScenarioDefinition> configuration)
        {
            var definition = new ScenarioDefinition();
            configuration(definition);

            return new BindingScenario<T>(definition);
        }

        #region Nested type: ScenarioDefinition

        public class ScenarioDefinition
        {
            private readonly IList<Action<IBindingContext>> _actions = new List<Action<IBindingContext>>();
            private readonly InMemoryRequestData _data = new InMemoryRequestData();
            private readonly BindingRegistry _registry = new BindingRegistry();
            private readonly InMemoryServiceLocator _services = new InMemoryServiceLocator();
            private IServiceLocator _customServices;

            public ScenarioDefinition()
            {
                Model = new T();

                // TODO -- like for all the binding log messages to come thru
                _services.Add<IObjectResolver>(new ObjectResolver(_services, _registry, new NulloBindingLogger()));
            }

            protected internal InMemoryRequestData RequestData
            {
                get { return _data; }
            }

            protected internal IServiceLocator Services
            {
                get { return _customServices ?? _services; }
            }

            protected internal IEnumerable<Action<IBindingContext>> Actions
            {
                get
                {
                    if (!_actions.Any())
                    {
                        return new Action<IBindingContext>[]{
                            context =>
                            new ObjectResolver(Services, Registry, new NulloBindingLogger()).BindModel(Model,
                                                                                                       context)
                        };
                        ;
                    }


                    return _actions;
                }
            }

            public T Model { get; set; }

            public BindingRegistry Registry
            {
                get { return _registry; }
            }

            public void ServicesFrom(IServiceLocator services)
            {
                _customServices = services;
            }

            public void Service<TService>(TService service)
            {
                if (_customServices != null)
                    throw new ArgumentOutOfRangeException("Cannot set services if using a pre-built IServiceLocator");

                _services.Add(service);
            }

            /// <summary>
            /// Allows you to force load key/value pairs in the format:
            /// prop1=val1
            /// ChildProp1=val
            ///      Prop2=val
            ///      Prop3=val      
            /// </summary>
            /// <param name="text"></param>
            public void Data(string text)
            {
                _data.ReadData(text);
            }

            public void Data(string name, object value)
            {
                _data[name] = value;
            }

            public void Data(Expression<Func<T, object>> property, object rawValue)
            {
                _data[property.ToAccessor().Name] = rawValue;
            }

            public void BindWith(IModelBinder binder)
            {
                _actions.Add(context => binder.Bind(typeof (T), Model, context));
            }

            public void BindWith<TBinder>() where TBinder : IModelBinder, new()
            {
                _actions.Add(context => new TBinder().Bind(typeof (T), Model, context));
            }

            public void BindPropertyWith<TBinder>(Expression<Func<T, object>> property, string rawValue = null)
                where TBinder : IPropertyBinder, new()
            {
                BindPropertyWith(new TBinder(), property, rawValue);
            }

            public void BindPropertyWith(IPropertyBinder binder, Expression<Func<T, object>> property,
                                         string rawValue = null)
            {
                if (rawValue != null) Data(property, rawValue);
                var prop = property.ToAccessor().InnerProperty;
                _actions.Add(context => StandardModelBinder.PopulatePropertyWithBinder(prop, context, binder));
            }
        }

        #endregion
    }
}