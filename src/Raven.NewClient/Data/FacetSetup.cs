using System.Collections.Generic;
using Raven.NewClient.Abstractions.Data;

namespace Raven.NewClient.Client.Data
{
    public class FacetSetup
    {
        /// <summary>
        /// Id of a facet setup document.
        /// </summary>
        public string Id { get; set; }
        
        /// <summary>
        /// List of facets.
        /// </summary>
        public List<Facet> Facets { get; set; }

        public FacetSetup()
        {
            Facets = new List<Facet>();
        }
    }
}
