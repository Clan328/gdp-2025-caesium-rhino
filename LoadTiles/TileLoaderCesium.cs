using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LoadTiles
{
    public class TileLoaderCesium : TileLoader
    {
        private string? rootUrl, accessToken;
        protected override string RootUrl => rootUrl ?? throw new InvalidOperationException("Root URL not set.");
        protected override string AssetId => "96188";
        public TileLoaderCesium()
        {
        }

        protected override void InitApiParameters(string cesiumIonApiKey)
        {
            // Fetch endpoint data from Cesium Ion API
            using JsonDocument doc = CallIonEndpoint(cesiumIonApiKey);
            rootUrl = doc.RootElement.GetProperty("url").ToString();
            accessToken = doc.RootElement.GetProperty("accessToken").ToString();
            // Set the OAuth token for the Cesium Tiles client
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
  }
}