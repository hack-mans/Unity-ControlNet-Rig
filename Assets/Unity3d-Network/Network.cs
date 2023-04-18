using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ru.mofrison.Unity3D
{
    public static class Network
    {
        private static async Task<UnityWebRequest> SendWebRequest(UnityWebRequest request, CancellationTokenSource cancelationToken = null, System.Action<float> progress = null)
        {
            while (!Caching.ready)
            {
                if (cancelationToken != null && cancelationToken.IsCancellationRequested)
                {
                    return null;
                }
                await Task.Yield();
            }

#pragma warning disable CS4014
            request.SendWebRequest();
#pragma warning restore CS4014

            while (!request.isDone)
            {
                if (cancelationToken != null && cancelationToken.IsCancellationRequested)
                {
                    request.Abort();
                    var url = request.url;
                    request.Dispose();
                    throw new Exception(string.Format("Netowrk.SendWebRequest - cancel download: {0}", url));
                }
                else
                {
                    progress?.Invoke(request.downloadProgress);
                    await Task.Yield();
                }
            }

            if (!request.isNetworkError) { progress?.Invoke(1f); }
            return request;
        }

        public static async Task<long> GetSize(string url)
        {
            UnityWebRequest request = await SendWebRequest(UnityWebRequest.Head(url));
            var contentLength = request.GetResponseHeader("Content-Length");
            if (long.TryParse(contentLength, out long returnValue))
            {
                return returnValue;
            }
            else
            {
                throw new Exception(string.Format("Netowrk.GetSize - {0} {1}", request.error, url));
            }
        }

        public static async Task<string> GetText(string url)
        {
            var uwr = await SendWebRequest(UnityWebRequest.Get(url));
            if (uwr != null && !uwr.isHttpError && !uwr.isNetworkError)
            {
                return uwr.downloadHandler.text;
            }
            else
            {
                throw new Exception(string.Format("Netowrk.GetText - {0} {1}", uwr.error, uwr.url));
            }
        }

        public static async Task<byte[]> GetData(string url, CancellationTokenSource cancelationToken, System.Action<float> progress = null)
        {
            UnityWebRequest uwr = await SendWebRequest(UnityWebRequest.Get(url), cancelationToken, progress);
            if (uwr != null && !uwr.isHttpError && !uwr.isNetworkError)
            {
                return uwr.downloadHandler.data;
            }
            else
            {
                throw new Exception(string.Format("Netowrk.GetData - {0} {1}", uwr.error, uwr.url));
            }
        }

        public static async Task<Texture2D> GetTexture(string url, CancellationTokenSource cancelationToken, System.Action<float> progress = null, bool caching = true)
        {
            string path = await url.GetPathOrUrl();
            bool isCached = path.Contains("file://");
            UnityWebRequest request = UnityWebRequestTexture.GetTexture(path);

            UnityWebRequest uwr = await SendWebRequest(request, cancelationToken, isCached? null : progress);
            if (uwr != null && !uwr.isHttpError && !uwr.isNetworkError)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(uwr);
                texture.name = Path.GetFileNameWithoutExtension(uwr.url);
                if (caching && !isCached) 
                {
                    try
                    {
                        ResourceCache.Caching(uwr.url, uwr.downloadHandler.data);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning(string.Format("Netowrk.GetTexture - {0}", e.Message));
                    }
                }
                return texture;
            }
            else
            {
                throw new Exception(string.Format("Netowrk.GetTexture - {0} {1}", uwr.error, uwr.url));
            }
        }

        public static async Task<AudioClip> GetAudioClip(string url, CancellationTokenSource cancelationToken, System.Action<float> progress = null, bool caching = true, AudioType audioType = AudioType.OGGVORBIS)
        {
            string path = await url.GetPathOrUrl();
            bool isCached = path.Contains("file://");
            UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(path, audioType);
        
            UnityWebRequest uwr = await SendWebRequest(request, cancelationToken, isCached ? null : progress);
            if (uwr != null && !uwr.isHttpError && !uwr.isNetworkError)
            {
                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(uwr);
                audioClip.name = Path.GetFileNameWithoutExtension(uwr.url);
                if (caching && !isCached)
                {
                    try
                    {
                        ResourceCache.Caching(uwr.url, uwr.downloadHandler.data);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning(string.Format("Netowrk.GetAudioClip - {0}", e.Message));
                    }
                }
                return audioClip;
            }
            else
            {
                throw new Exception(string.Format("Netowrk.GetAudioClip - {0} {1}", uwr.error, uwr.url));
            }
        }

        private delegate void AsyncOperation();

        public static async Task<string> GetVideoStream(string url, CancellationTokenSource cancelationToken, System.Action<float> progress = null, bool caching = true, bool preload = false)
        {
            if (!caching) return url;
            string path = await url.GetPathOrUrl();
            if (!path.Contains("file://"))
            {
                if (preload)
                {
                    return await CahingData(url, cancelationToken, progress);
                }
                else
                {
                    AsyncOperation cachingVideo = async delegate {
                        await CahingData(url, cancelationToken);
                    };
                    cachingVideo();
                    return url;
                }
            }
            else { return path; }
        }

        private static async Task<string> CahingData(string url, CancellationTokenSource cancelationToken, System.Action<float> progress = null)
        {
            if (ResourceCache.CheckFreeSpace(await GetSize(url)))
            {
                return ResourceCache.Caching(url, await GetData(url, cancelationToken, progress));
            } else return null;
        }

        public static async Task<AssetBundle> GetAssetBundle(string url, CancellationTokenSource cancelationToken, System.Action<float> progress = null, bool caching = true)
        {
            UnityWebRequest request;
            CachedAssetBundle assetBundleVersion = await GetAssetBundleVersion(url);
            
            if (Caching.IsVersionCached(assetBundleVersion) || (caching && ResourceCache.CheckFreeSpace(await GetSize(url))))
            {
                request = UnityWebRequestAssetBundle.GetAssetBundle(url, assetBundleVersion, 0);
            }
            else 
            {
                request = UnityWebRequestAssetBundle.GetAssetBundle(url);
            }

            UnityWebRequest uwr = await SendWebRequest(request, cancelationToken, Caching.IsVersionCached(assetBundleVersion) ? null : progress);
            if (uwr != null && !uwr.isHttpError && !uwr.isNetworkError)
            {
                AssetBundle assetBundle = DownloadHandlerAssetBundle.GetContent(uwr);
                if (caching) 
                {
                    // Deleting old versions from the cache
                    Caching.ClearOtherCachedVersions(assetBundle.name, assetBundleVersion.hash);
                }
                return assetBundle;
            }
            else
            {
                throw new Exception(string.Format("Netowrk.GetAssetBundle - {0} {1}", uwr.error, uwr.url));
            }
        }

        private static async Task<CachedAssetBundle> GetAssetBundleVersion(string url)
        {
            Hash128 hash = default;
            string localPath = new System.Uri(url).LocalPath;
            try
            {
                string manifest = await GetText(url + ".manifest");
                hash = manifest.GetHash();
                return new CachedAssetBundle(localPath, hash);
            }
            catch (Exception e)
            {
                Debug.LogWarning(string.Format("Netowrk.GetAssetBundleVersion - {0}", e.Message));
                DirectoryInfo dir = new DirectoryInfo(url.ConvertToCachedPath());
                if (dir.Exists)
                {
                    System.DateTime lastWriteTime = default;
                    var dirs = dir.GetDirectories();
                    for (int i=0; i < dirs.Length; i++)
                    {
                        if (lastWriteTime < dirs[i].LastWriteTime)
                        {
                            if (hash.isValid && hash != default) 
                            { 
                                Directory.Delete(Path.Combine(dir.FullName, hash.ToString()), true);
                            }
                            lastWriteTime = dirs[i].LastWriteTime;
                            hash = Hash128.Parse(dirs[i].Name);
                        }
                        else { Directory.Delete(Path.Combine(dir.FullName, dirs[i].Name), true); }
                    }
                    return new CachedAssetBundle(localPath, hash);
                }
                else
                {
                    throw new Exception(string.Format("Netowrk.GetAssetBundleVersion - Nothing was found in the cache for {0}", url));
                }
            }
        }

        private static Hash128 GetHash(this string str)
        {
            var hashRow = str.Split("\n".ToCharArray())[5];
            var hash = Hash128.Parse(hashRow.Split(':')[1].Trim());
            if (hash.isValid && hash != default) { return hash; }
            else { throw new Exception("Netowrk.GetHash - Couldn't extract hash from manifest."); }
        }

        private static async Task<string> GetPathOrUrl(this string url)
        {
            string path = url.ConvertToCachedPath();
            if (File.Exists(path)) {
                try
                {
                    if (new FileInfo(path).Length != await GetSize(url)) { return url; }
                }
                catch (Exception e)
                {
                    Debug.LogWarning(string.Format("Netowrk.GetPathOrUrl - {0}", e.Message)); 
                }
                return "file://" + path;
            }
            else return url;
        }

        public class Exception : System.Exception
        {
            public Exception(string message) : base(message) { }
        }
    }
}