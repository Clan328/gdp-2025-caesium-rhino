using System;
using Rhino.Geometry;

namespace LoadTiles;

public static class Helper {

    // WGS84 ellipsoid parameters
    private const double EQ_RADIUS = 6378137.0; // Equatorial radius in meters
    private const double FLATTENING = 1 / 298.257223563; // Flattening
    private const double SQ_FST_ECCENTRICITY = (2 - FLATTENING) * FLATTENING; // First eccentricity squared
    
    public static Point3d LatLonToEPSG4978(double latitude, double longitude, double altitude = 0)
    {
        // Convert degrees to radians
        double latRad = latitude * Math.PI / 180.0;
        double lonRad = longitude * Math.PI / 180.0;

        // Compute the prime vertical radius of curvature
        double N = EQ_RADIUS / Math.Sqrt(1 - SQ_FST_ECCENTRICITY * Math.Pow(Math.Sin(latRad), 2));

        // Compute ECEF coordinates
        double X = (N + altitude) * Math.Cos(latRad) * Math.Cos(lonRad);
        double Y = (N + altitude) * Math.Cos(latRad) * Math.Sin(lonRad);
        double Z = ((1 - SQ_FST_ECCENTRICITY) * N + altitude) * Math.Sin(latRad);

        return new Point3d(X, Y, Z);
    }
}