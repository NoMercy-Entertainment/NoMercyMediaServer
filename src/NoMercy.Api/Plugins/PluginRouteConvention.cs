// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using NoMercy.Plugins.Mvc;

namespace NoMercy.Api.Plugins;

/// <summary>
/// Puts every plugin controller under <c>api/plugins/{pluginId}</c>.
/// <para>
/// A plugin does not get to choose where its routes land. Left to itself it
/// would eventually claim <c>api/v1/movies</c> — by accident or otherwise — and
/// shadow the server's own endpoint for every client on the network. The prefix
/// is applied here, from the assembly the controller came from, so a plugin
/// cannot opt out of it by writing a different <c>[Route]</c>.
/// </para>
/// </summary>
public class PluginRouteConvention(IPluginAssemblyCatalog catalog) : IApplicationModelConvention
{
    public void Apply(ApplicationModel application)
    {
        foreach (ControllerModel controller in application.Controllers)
        {
            if (!typeof(PluginControllerBase).IsAssignableFrom(controller.ControllerType))
                continue;

            Guid? owner = catalog.OwnerOf(controller.ControllerType.Assembly);

            if (owner is null)
                continue;

            // The literal id, not a route parameter. With a parameter every
            // plugin controller would share one template, so two plugins each
            // defining "status" would be an ambiguous match, and a client could
            // reach one plugin's controller through another plugin's path.
            // Versioned like every other endpoint the clients call. Left
            // unversioned the plugin surface could never be versioned later
            // without breaking plugins already in the field.
            AttributeRouteModel prefix = new(
                new RouteAttribute($"api/v{{version:apiVersion}}/plugins/{owner}")
            );

            // Carried as a route value so the base class can read it. Sourced
            // from the assembly the controller came from, never from the URL,
            // which is what makes it something a caller cannot lie about.
            controller.RouteValues["pluginId"] = owner.ToString();

            foreach (SelectorModel selector in controller.Selectors)
            {
                selector.AttributeRouteModel = selector.AttributeRouteModel is null
                    ? prefix
                    : AttributeRouteModel.CombineAttributeRouteModel(
                        prefix,
                        selector.AttributeRouteModel
                    );
            }
        }
    }
}
