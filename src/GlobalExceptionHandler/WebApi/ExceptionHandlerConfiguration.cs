﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace GlobalExceptionHandler.WebApi
{
	public class ExceptionHandlerConfiguration
	{
		readonly IDictionary<Type, ExceptionConfig> _exceptionConfiguration = new Dictionary<Type, ExceptionConfig>();

		Type[] _exceptionConfgurationTypesSortedByDepthDescending;

		public ExceptionHandlerConfiguration(Func<Exception, HttpContext, Task> defaultFormatter) => DefaultFormatter = defaultFormatter;

		public IHasStatusCode ForException<T>() where T : Exception
		{
			var type = typeof(T);
			return new ExceptionRuleCreator(_exceptionConfiguration, type);
		}

		public void UseDefaultMessageFormatter(Func<Exception, HttpContext, Task> formatter)
		{
			DefaultFormatter = formatter;
		}

		internal IDictionary<Type, ExceptionConfig> ExceptionConfiguration => _exceptionConfiguration;

		internal Func<Exception, HttpContext, Task> DefaultFormatter { get; private set; }

		internal RequestDelegate BuildHandler()
		{
			_exceptionConfgurationTypesSortedByDepthDescending = _exceptionConfiguration.Keys
				.OrderBy(x => x, new ExceptionTypePolymorphicComparer())
				.ToArray();

			return async context =>
			{
				var exception = context.Features.Get<IExceptionHandlerFeature>().Error;

				foreach (var type in _exceptionConfgurationTypesSortedByDepthDescending)
				{
					// ReSharper disable once UseMethodIsInstanceOfType TODO: Fire those guys
					if (type.IsAssignableFrom(exception.GetType()))
					{
						var config = ExceptionConfiguration[type];
						context.Response.StatusCode = (int)config.StatusCode;
						await config.Formatter(exception, context);
						return;
					}
				}
			};
		}
	}
}