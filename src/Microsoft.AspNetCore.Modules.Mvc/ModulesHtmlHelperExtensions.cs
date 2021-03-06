﻿using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Modules;

namespace Microsoft.AspNetCore.Modules.Mvc
{
    public static class ModulesHtmlHelperExtensions
    {
        public static Task<IHtmlContent> ModulePartialAsync(this IHtmlHelper htmlHelper, string partialViewName, string moduleInstanceId)
        {
            return ModulePartialAsync(htmlHelper, partialViewName, moduleInstanceId, htmlHelper.ViewData.Model, viewData: null);
        }

        public static Task<IHtmlContent> ModulePartialAsync(this IHtmlHelper htmlHelper, string partialViewName, string moduleInstanceId, object model, ViewDataDictionary viewData)
        {
            var viewContext = htmlHelper.ViewContext;
            var env = viewContext.HttpContext.RequestServices.GetService<IHostingEnvironment>();
            if (env.ApplicationName == moduleInstanceId)
            {
                return htmlHelper.PartialAsync(partialViewName, model, viewData);
            }

            var moduleManager = viewContext.HttpContext.RequestServices.GetRequiredService<IModuleManager>();
            var module = moduleManager.GetModuleInstance(moduleInstanceId);
            var moduleHtmlHelper = module.ModuleServices.GetService<IHtmlHelper>();
            var moduleViewContext = new ViewContext(viewContext, viewContext.View, viewContext.ViewData, viewContext.Writer);
            moduleViewContext.HttpContext = new ModuleHttpContext(viewContext.HttpContext.Features, module.ModuleServices);
            (moduleHtmlHelper as IViewContextAware)?.Contextualize(moduleViewContext);
            return moduleHtmlHelper.PartialAsync(partialViewName, model, viewData);
        }

        public static Task<IHtmlContent> ModulePartialAsync(this IHtmlHelper htmlHelper, string partialViewName)
        {
            return ModulePartialAsync(htmlHelper, partialViewName, htmlHelper.ViewData.Model, viewData: null);
        }

        public static async Task<IHtmlContent> ModulePartialAsync(this IHtmlHelper htmlHelper, string partialViewName, object model, ViewDataDictionary viewData)
        {
            var viewContext = htmlHelper.ViewContext;
            var moduleManager = viewContext.HttpContext.RequestServices.GetService<IModuleManager>();
            
            // TODO: Add real partial view discovery logic here that doesn't rely on exception handling
            try
            {
                return await htmlHelper.PartialAsync(partialViewName, model, viewData);
            }
            catch (InvalidOperationException)
            {
                foreach (var module in moduleManager.GetModuleDescriptors())
                {
                    try
                    {
                        return await htmlHelper.ModulePartialAsync(partialViewName, module.Name, model, viewData);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
                throw;
            }
        }

        public static IHtmlContent ModulePartial(this IHtmlHelper htmlHelper, string partialViewName, string moduleName)
        {
            var result = ModulePartialAsync(htmlHelper, partialViewName, moduleName);
            return result.GetAwaiter().GetResult();
        }

        public static IHtmlContent ModulePartial(this IHtmlHelper htmlHelper, string partialViewName)
        {
            var result = ModulePartialAsync(htmlHelper, partialViewName);
            return result.GetAwaiter().GetResult();
        }

        public static IHtmlContent ModuleActionLink(
            this IHtmlHelper htmlHelper,
            string linkText,
            string actionName,
            string controllerName,
            string moduleName)
        {
            return ModuleActionLink(
                htmlHelper, 
                linkText, 
                actionName, 
                controllerName, 
                moduleName, 
                protocol: null,
                hostname: null,
                fragment: null,
                routeValues: null,
                htmlAttributes: null);
        }

        public static IHtmlContent ModuleActionLink(
            this IHtmlHelper htmlHelper,
            string linkText,
            string actionName,
            string controllerName,
            string moduleInstanceId,
            string protocol,
            string hostname,
            string fragment,
            object routeValues,
            object htmlAttributes)
        {
            if (linkText == null)
            {
                throw new ArgumentNullException(nameof(linkText));
            }

            var viewContext = htmlHelper.ViewContext;
            var moduleManager = viewContext.HttpContext.RequestServices.GetService<IModuleManager>();
            var module = moduleManager.GetModuleInstance(moduleInstanceId);

            var moduleViewContext = new ViewContext(viewContext, viewContext.View, viewContext.ViewData, viewContext.Writer);
            moduleViewContext.RouteData = new RouteData(moduleViewContext.RouteData);
            var sharedRoutesManager = module.ModuleServices.GetService<ISharedRoutesManager>();
            var moduleRouteBuilder = sharedRoutesManager.GetRoutes(moduleInstanceId);
            moduleViewContext.RouteData.Routers.Add(moduleRouteBuilder.Build());
            moduleViewContext.HttpContext = new ModuleHttpContext(
                viewContext.HttpContext.Features, module.ModuleServices, module.PathBase);

            var moduleHtmlGenerator = module.ModuleServices.GetService<IHtmlGenerator>();
            var tagBuilder = moduleHtmlGenerator.GenerateActionLink(
                moduleViewContext,
                linkText,
                actionName,
                controllerName,
                protocol,
                hostname,
                fragment,
                routeValues,
                htmlAttributes);
            if (tagBuilder == null)
            {
                return HtmlString.Empty;
            }

            return tagBuilder;
        }
    }

}
