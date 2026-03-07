using System.Collections.Generic;
using System.Xml;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class GPXReader : MonoBehaviour
{
    public string fileName = "activity_21348500211.gpx";

    public List<float> elevations = new List<float>();
    public List<float> distances = new List<float>();

    // --- new data structures for GPS points and conversion ---
    public class GPXPoint
    {
        public float lat;
        public float lon;
        public float ele;
        public Vector3 unityPosition;
    }

    /// <summary>
    /// list of raw points read from GPX file (latitude, longitude, elevation)
    /// </summary>
    public List<GPXPoint> gpxPoints = new List<GPXPoint>();

    // origin point used for converting to local coordinates
    private GPXPoint gpxOrigin = null;

    // optional prefab used for visualizing individual points
    public GameObject pointPrefab;

    // internal line renderer used to draw the path
    private LineRenderer lineRenderer;

    float lastLat = 0f;
    float lastLon = 0f;

    private float HaversineDistance(float lat1, float lon1, float lat2, float lon2)
    {
        const float R = 6371000f; // rayon de la Terre en mètres
        float dLat = Mathf.Deg2Rad * (lat2 - lat1);
        float dLon = Mathf.Deg2Rad * (lon2 - lon1);

        float a = Mathf.Sin(dLat / 2f) * Mathf.Sin(dLat / 2f) +
                Mathf.Cos(Mathf.Deg2Rad * lat1) * Mathf.Cos(Mathf.Deg2Rad * lat2) *
                Mathf.Sin(dLon / 2f) * Mathf.Sin(dLon / 2f);

        float c = 2f * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1f - a));
        return R * c; // distance en mètres
    }

    public void LoadGPX()
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            Debug.LogWarning("fileName GPX vide, recherche automatique d'un .gpx dans StreamingAssets");
            var files = System.IO.Directory.GetFiles(Application.streamingAssetsPath, "*.gpx");
            if (files.Length > 0)
            {
                fileName = System.IO.Path.GetFileName(files[0]);
                Debug.Log("Utilisation automatique du fichier : " + fileName);
            }
            else
            {
                Debug.LogError("Aucun fichier .gpx trouvé dans StreamingAssets");
                return;
            }
        }

        string path = fileName;
        if (!System.IO.Path.IsPathRooted(path))
            path = System.IO.Path.Combine(Application.streamingAssetsPath, fileName);

        Debug.Log("Chargement GPX depuis : " + path);

        if (!System.IO.File.Exists(path))
        {
            Debug.LogError("GPX introuvable : " + path);
            return;
        }

        XmlDocument doc = new XmlDocument();
        try
        {
            doc.Load(path);
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Erreur de lecture GPX : " + ex.Message);
            return;
        }

        XmlNodeList nodes = doc.GetElementsByTagName("trkpt");
        Debug.Log($"Nombre de trkpt lus : {nodes.Count}");

        float totalDistance = 0f;
        Vector3 lastPoint = Vector3.zero;
        float ele = 0f; // elevation of the current point

        distances.Clear();
        elevations.Clear();
        gpxPoints.Clear();
        gpxOrigin = null;

        foreach (XmlNode node in nodes)
        {
            float lat = float.Parse(node.Attributes["lat"].Value, System.Globalization.CultureInfo.InvariantCulture);
            float lon = float.Parse(node.Attributes["lon"].Value, System.Globalization.CultureInfo.InvariantCulture);
            ele = float.Parse(node["ele"].InnerText, System.Globalization.CultureInfo.InvariantCulture);

            float deltaDist = 0f;

            // add GPS point to list
            GPXPoint p = new GPXPoint { lat = lat, lon = lon, ele = ele };
            gpxPoints.Add(p);

            if (lastPoint != Vector3.zero)
            {
                deltaDist = HaversineDistance(lastLat, lastLon, lat, lon);
                totalDistance += deltaDist;
            }

            distances.Add(totalDistance);
            elevations.Add(ele);

            lastLat = lat;
            lastLon = lon;
            lastPoint = new Vector3(lat, ele, lon);
        }

        Debug.Log($"Point {distances.Count}: lat={lastLat}, lon={lastLon}, ele={ele}, totalDistance={totalDistance}");
        for (int i = 1; i < elevations.Count - 1; i++)
        {
            elevations[i] = (elevations[i - 1] + elevations[i] + elevations[i + 1]) / 3f;
        }

        Debug.Log("GPX chargé : " + elevations.Count + " points.");

        // origin declaration and local conversion
        if (gpxPoints.Count > 0)
        {
            gpxOrigin = gpxPoints[0];
            gpxOrigin.unityPosition = Vector3.zero;
            ComputeUnityPositions();
        }
    }

    // ------------------------------------------------------------------
    // geometry helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// compute a local Unity position for every point relative to origin
    /// </summary>
    public void ComputeUnityPositions()
    {
        if (gpxOrigin == null || gpxPoints.Count == 0)
            return;

        for (int i = 1; i < gpxPoints.Count; i++)
        {
            GPXPoint p = gpxPoints[i];
            // east-west distance in meters
            float east = HaversineDistance(gpxOrigin.lat, gpxOrigin.lon, gpxOrigin.lat, p.lon);
            if (p.lon < gpxOrigin.lon) east = -east;
            // north-south distance in meters
            float north = HaversineDistance(gpxOrigin.lat, gpxOrigin.lon, p.lat, gpxOrigin.lon);
            if (p.lat < gpxOrigin.lat) north = -north;
            float y = p.ele - gpxOrigin.ele;
            p.unityPosition = new Vector3(east, y, north);
            gpxPoints[i] = p;
        }
        // origin stays at zero
    }

    /// <summary>
    /// return a Unity world position corresponding to a travelled distance along the route
    /// </summary>
    public Vector3 GetUnityPositionAtDistance(float distance)
    {
        if (distances.Count < 2 || gpxPoints.Count < 2)
            return Vector3.zero;

        if (distance <= 0f)
            return gpxPoints[0].unityPosition;

        float lastDist = distances[distances.Count - 1];
        if (distance >= lastDist)
            return gpxPoints[gpxPoints.Count - 1].unityPosition;

        for (int i = 1; i < distances.Count; i++)
        {
            if (distance < distances[i])
            {
                float t = (distance - distances[i - 1]) / (distances[i] - distances[i - 1]);
                return Vector3.Lerp(gpxPoints[i - 1].unityPosition, gpxPoints[i].unityPosition, t);
            }
        }
        return Vector3.zero;
    }

    /// <summary>
    /// draw the stored route; either a line or cubes if a prefab is assigned
    /// </summary>
    public void DrawRoute(bool useLine = true)
    {
        if (gpxPoints.Count < 2)
            return;

        if (useLine)
        {
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.GetComponent<LineRenderer>();
                if (lineRenderer == null)
                    lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            if (lineRenderer != null)
            {
                lineRenderer.positionCount = gpxPoints.Count;
                for (int i = 0; i < gpxPoints.Count; i++)
                    lineRenderer.SetPosition(i, gpxPoints[i].unityPosition);
                lineRenderer.startWidth = lineRenderer.endWidth = 0.1f;
            }
            else
            {
                Debug.LogWarning("Impossible de créer/obtenir LineRenderer pour dessiner la route");
            }
        }
        else if (pointPrefab != null)
        {
            foreach (var pt in gpxPoints)
            {
                Instantiate(pointPrefab, pt.unityPosition, Quaternion.identity);
            }
        }
    }

    // ------------------------------------------------------------------
    
    private void Start()
    {
        // ensure line renderer reference exists (RequireComponent should guarantee it, but just in case)
        lineRenderer = GetComponent<LineRenderer>();

        // automatic behaviour for quick testing
        // only load once if not already done earlier by another script
        if (gpxPoints == null || gpxPoints.Count == 0)
            LoadGPX();

        DrawRoute(true);
    }

    private void OnDrawGizmos()
    {
        if (gpxPoints == null || gpxPoints.Count < 2)
            return;

        Gizmos.color = Color.yellow;
        for (int i = 1; i < gpxPoints.Count; i++)
        {
            Gizmos.DrawLine(gpxPoints[i - 1].unityPosition, gpxPoints[i].unityPosition);
        }
    }

    public float GetSlopeAtDistance(float currentDistance)
    {
        if (distances.Count < 2)
        {
            Debug.LogWarning($"GetSlopeAtDistance appelé mais distances.Count={distances.Count}");
            return 0f;
        }

        // si on est au-delà de la fin de la trace, renvoyer 0 et logguer une seule fois
        float lastDist = distances[distances.Count - 1];
        if (currentDistance >= lastDist)
        {
            Debug.Log($"GetSlopeAtDistance : distance {currentDistance:F1} >= dernier point {lastDist:F1}, slope=0");
            return 0f;
        }

        for (int i = 1; i < distances.Count; i++)
        {
            if (currentDistance < distances[i])
            {
                float deltaEle = elevations[i] - elevations[i - 1];
                float deltaDist = distances[i] - distances[i - 1];

                if (deltaDist == 0)
                {
                    Debug.Log($"Segment {i-1}->{i} distance identique, slope=0");
                    return 0f;
                }

                float slope = deltaEle / deltaDist;
                Debug.Log($"slope calc pour dist {currentDistance:F1} sur segment {i} = {slope*100:F2}%");
                return slope;
            }
        }

        // théoriquement on ne devrait jamais y arriver
        Debug.LogError($"GetSlopeAtDistance : aucune tranche trouvée pour {currentDistance:F1}");
        return 0f;
    }
}