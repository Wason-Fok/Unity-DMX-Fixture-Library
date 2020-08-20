// #define QUICKSEARCH_DEBUG
using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using UnityEditorInternal;

#if UNITY_2020_1_OR_NEWER
using UnityEngine.UIElements;
#endif

[assembly: InternalsVisibleTo("com.unity.quicksearch.tests")]

namespace Unity.QuickSearch
{
    internal static class Utils
    {
        private static string[] _ignoredAssemblies =
        {
            "^UnityScript$", "^System$", "^mscorlib$", "^netstandard$",
            "^System\\..*", "^nunit\\..*", "^Microsoft\\..*", "^Mono\\..*", "^SyntaxTree\\..*"
        };

        private static Type[] GetAllEditorWindowTypes()
        {
            return GetAllDerivedTypes(AppDomain.CurrentDomain, typeof(EditorWindow));
        }

        internal static Type GetProjectBrowserWindowType()
        {
            return GetAllEditorWindowTypes().FirstOrDefault(t => t.Name == "ProjectBrowser");
        }

        internal static string GetNameFromPath(string path)
        {
            var lastSep = path.LastIndexOf('/');
            if (lastSep == -1)
                return path;

            return path.Substring(lastSep + 1);
        }

        public static Texture2D GetAssetThumbnailFromPath(string path)
        {
            Texture2D thumbnail = AssetDatabase.GetCachedIcon(path) as Texture2D;
            return thumbnail ?? UnityEditorInternal.InternalEditorUtility.FindIconForFile(path);
        }

        public static Texture2D GetAssetPreviewFromPath(string path, Vector2 previewSize, FetchPreviewOptions previewOptions)
        {
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (obj == null)
                return null;
            var preview = AssetPreview.GetAssetPreview(obj);
            if (preview == null || previewOptions.HasFlag(FetchPreviewOptions.Large))
            {
                var largePreview = AssetPreview.GetMiniThumbnail(obj);
                if (preview == null || (largePreview != null && largePreview.width > preview.width))
                    preview = largePreview;
            }
            return preview;
        }

        public static UnityEngine.Object SelectAssetFromPath(string path, bool ping = false)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset != null)
            {
                Selection.activeObject = asset;
                if (ping)
                    EditorGUIUtility.PingObject(asset);
            }
            return asset;
        }

        public static void FrameAssetFromPath(string path)
        {
            var asset = SelectAssetFromPath(path);
            if (asset != null)
            {
                EditorApplication.delayCall += () =>
                {
                    EditorWindow.FocusWindowIfItsOpen(Utils.GetProjectBrowserWindowType());
                    EditorApplication.delayCall += () => EditorGUIUtility.PingObject(asset);
                };
            }
            else
            {
                EditorUtility.RevealInFinder(path);
            }
        }

        public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }

        internal static Type[] GetAllDerivedTypes(this AppDomain aAppDomain, Type aType)
        {
            #if UNITY_2019_2_OR_NEWER
            return TypeCache.GetTypesDerivedFrom(aType).ToArray();
            #else
            var result = new List<Type>();
            var assemblies = aAppDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var types = assembly.GetLoadableTypes();
                foreach (var type in types)
                {
                    if (type.IsSubclassOf(aType))
                        result.Add(type);
                }
            }
            return result.ToArray();
            #endif
        }

        internal static string FormatProviderList(IEnumerable<SearchProvider> providers, bool fullTimingInfo = false)
        {
            return string.Join(fullTimingInfo ? "\r\n" : ", ", providers.Select(p =>
            {
                var avgTime = p.avgTime;
                if (fullTimingInfo)
                    return $"{p.name.displayName} ({avgTime:0.#} ms, Enable: {p.enableTime:0.#} ms, Init: {p.loadTime:0.#} ms)";
             
                var avgTimeLabel = String.Empty;
                if (avgTime > 9.99)
                    avgTimeLabel = $" ({avgTime:#} ms)";
                return $"<b>{p.name.displayName}</b>{avgTimeLabel}";
            }));
        }

        private static bool IsIgnoredAssembly(AssemblyName assemblyName)
        {
            var name = assemblyName.Name;
            return _ignoredAssemblies.Any(candidate => Regex.IsMatch(name, candidate));
        }

        internal static MethodInfo[] GetAllStaticMethods(this AppDomain aAppDomain, bool showInternalAPIs)
        {
            var result = new List<MethodInfo>();
            var assemblies = aAppDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                if (IsIgnoredAssembly(assembly.GetName()))
                    continue;
                #if QUICKSEARCH_DEBUG
                var countBefore = result.Count;
                #endif
                var types = assembly.GetLoadableTypes();
                foreach (var type in types)
                {
                    var methods = type.GetMethods(BindingFlags.Static | (showInternalAPIs ? BindingFlags.Public | BindingFlags.NonPublic : BindingFlags.Public) | BindingFlags.DeclaredOnly);
                    foreach (var m in methods)
                    {
                        if (m.IsPrivate)
                            continue;

                        if (m.IsGenericMethod)
                            continue;

                        if (m.Name.Contains("Begin") || m.Name.Contains("End"))
                            continue;

                        if (m.GetParameters().Length == 0)
                            result.Add(m);
                    }
                }
                #if QUICKSEARCH_DEBUG
                Debug.Log($"{result.Count - countBefore} - {assembly.GetName()}");
                #endif
            }
            return result.ToArray();
        }

        static UnityEngine.Object s_MainWindow = null;
        internal static Rect GetEditorMainWindowPos()
        {
            if (s_MainWindow == null)
            {
                var containerWinType = AppDomain.CurrentDomain.GetAllDerivedTypes(typeof(ScriptableObject)).FirstOrDefault(t => t.Name == "ContainerWindow");
                if (containerWinType == null)
                    throw new MissingMemberException("Can't find internal type ContainerWindow. Maybe something has changed inside Unity");
                var showModeField = containerWinType.GetField("m_ShowMode", BindingFlags.NonPublic | BindingFlags.Instance);
                if (showModeField == null)
                    throw new MissingFieldException("Can't find internal fields 'm_ShowMode'. Maybe something has changed inside Unity");
                var windows = Resources.FindObjectsOfTypeAll(containerWinType);
                foreach (var win in windows)
                {
                    var showMode = (int)showModeField.GetValue(win);
                    if (showMode == 4) // main window
                    {
                        s_MainWindow = win;
                        break;
                    }
                }
            }

            if (s_MainWindow == null)
                return new Rect(0, 0, 800, 600);

            var positionProperty = s_MainWindow.GetType().GetProperty("position", BindingFlags.Public | BindingFlags.Instance);
            if (positionProperty == null)
                throw new MissingFieldException("Can't find internal fields 'position'. Maybe something has changed inside Unity.");
            return (Rect)positionProperty.GetValue(s_MainWindow, null);
        }

        internal static Rect GetCenteredWindowPosition(Rect parentWindowPosition, Vector2 size)
        {
            var pos = new Rect
            {
                x = 0, y = 0,
                width = Mathf.Min(size.x, parentWindowPosition.width * 0.90f), 
                height = Mathf.Min(size.y, parentWindowPosition.height * 0.90f)
            };
            var w = (parentWindowPosition.width - pos.width) * 0.5f;
            var h = (parentWindowPosition.height - pos.height) * 0.5f;
            pos.x = parentWindowPosition.x + w;
            pos.y = parentWindowPosition.y + h;
            return pos;
        }

        internal static IEnumerable<MethodInfo> GetAllMethodsWithAttribute<T>(BindingFlags bindingFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) where T : System.Attribute
        {
            #if UNITY_2019_2_OR_NEWER
            return TypeCache.GetMethodsWithAttribute<T>();
            #else
            Assembly assembly = typeof(Selection).Assembly;
            var managerType = assembly.GetTypes().First(t => t.Name == "EditorAssemblies");
            var method = managerType.GetMethod("Internal_GetAllMethodsWithAttribute", BindingFlags.NonPublic | BindingFlags.Static);
            var arguments = new object[] { typeof(T), bindingFlags };
            return ((method.Invoke(null, arguments) as object[]) ?? throw new InvalidOperationException()).Cast<MethodInfo>();
            #endif
        }

        internal static Rect GetMainWindowCenteredPosition(Vector2 size)
        {
            var mainWindowRect = GetEditorMainWindowPos();
            return GetCenteredWindowPosition(mainWindowRect, size);
        }

        internal static void ShowDropDown(this EditorWindow window, Vector2 size)
        {
            window.maxSize = window.minSize = size;
            window.position = GetMainWindowCenteredPosition(size);
            window.ShowPopup();

            Assembly assembly = typeof(EditorWindow).Assembly;

            var editorWindowType = typeof(EditorWindow);
            var hostViewType = assembly.GetType("UnityEditor.HostView");
            var containerWindowType = assembly.GetType("UnityEditor.ContainerWindow");

            var parentViewField = editorWindowType.GetField("m_Parent", BindingFlags.Instance | BindingFlags.NonPublic);
            var parentViewValue = parentViewField.GetValue(window);

            hostViewType.InvokeMember("AddToAuxWindowList", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.InvokeMethod, null, parentViewValue, null);

            // Dropdown windows should not be saved to layout
            var containerWindowProperty = hostViewType.GetProperty("window", BindingFlags.Instance | BindingFlags.Public);
            var parentContainerWindowValue = containerWindowProperty.GetValue(parentViewValue);
            var dontSaveToLayoutField = containerWindowType.GetField("m_DontSaveToLayout", BindingFlags.Instance | BindingFlags.NonPublic);
            dontSaveToLayoutField.SetValue(parentContainerWindowValue, true);
            Debug.Assert((bool) dontSaveToLayoutField.GetValue(parentContainerWindowValue));
        }

        internal static string JsonSerialize(object obj)
        {
            var assembly = typeof(Selection).Assembly;
            var managerType = assembly.GetTypes().First(t => t.Name == "Json");
            var method = managerType.GetMethod("Serialize", BindingFlags.Public | BindingFlags.Static);
            var jsonString = "";
            if (UnityVersion.IsVersionGreaterOrEqual(2019, 1, UnityVersion.ParseBuild("0a10")))
            {
                var arguments = new object[] { obj, false, "  " };
                jsonString = method.Invoke(null, arguments) as string;
            }
            else
            {
                var arguments = new object[] { obj };
                jsonString = method.Invoke(null, arguments) as string;
            }
            return jsonString;
        }

        internal static object JsonDeserialize(object obj)
        {
            Assembly assembly = typeof(Selection).Assembly;
            var managerType = assembly.GetTypes().First(t => t.Name == "Json");
            var method = managerType.GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Static);
            var arguments = new object[] { obj };
            return method.Invoke(null, arguments);
        }

        private static MethodInfo s_GetNumCharactersThatFitWithinWidthMethod;
        internal static int GetNumCharactersThatFitWithinWidth(GUIStyle style, string text, float width)
        {
            #if UNITY_2019_1_OR_NEWER
            if (s_GetNumCharactersThatFitWithinWidthMethod == null)
            {
                var kType = typeof(GUIStyle);
                s_GetNumCharactersThatFitWithinWidthMethod = kType.GetMethod("Internal_GetNumCharactersThatFitWithinWidth", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            var arguments = new object[] { text, width };
            return (int)s_GetNumCharactersThatFitWithinWidthMethod.Invoke(style, arguments);
            #else
            style.CalcMinMaxWidth(new GUIContent(text), out var minWidth, out _);
            return (int)(width / (minWidth / text.Length)) - 3;
            #endif
        }

        private static MethodInfo s_GetPackagesPathsMethod;
        internal static string[] GetPackagesPaths()
        {
            if (s_GetPackagesPathsMethod == null)
            {
                Assembly assembly = typeof(UnityEditor.PackageManager.Client).Assembly;
                var type = assembly.GetTypes().First(t => t.FullName == "UnityEditor.PackageManager.Folders");
                s_GetPackagesPathsMethod = type.GetMethod("GetPackagesPaths", BindingFlags.Public | BindingFlags.Static);
            }
            return (string[])s_GetPackagesPathsMethod.Invoke(null, null);
        }

        internal static string GetQuickSearchVersion()
        {
            string version = null;
            try
            {
                var filePath = File.ReadAllText($"{QuickSearch.packageFolderName}/package.json");
                if (JsonDeserialize(filePath) is Dictionary<string, object> manifest && manifest.ContainsKey("version"))
                {
                    version = manifest["version"] as string;
                }
            }
            catch (Exception)
            {
                // ignored
            }

            return version ?? "unknown";
        }
        internal static string GetNextWord(string src, ref int index)
        {
            // Skip potential white space BEFORE the actual word we are extracting
            for (; index < src.Length; ++index)
            {
                if (!char.IsWhiteSpace(src[index]))
                {
                    break;
                }
            }

            var startIndex = index;
            for (; index < src.Length; ++index)
            {
                if (char.IsWhiteSpace(src[index]))
                {
                    break;
                }
            }

            return src.Substring(startIndex, index - startIndex);
        }

        internal static bool IsDeveloperMode()
        {
            #if QUICKSEARCH_DEBUG
            return true;
            #else
            return Directory.Exists($"{QuickSearch.packageFolderName}/.git");
            #endif
        }

        public static int LevenshteinDistance<T>(IEnumerable<T> lhs, IEnumerable<T> rhs) where T : System.IEquatable<T>
        {
            if (lhs == null) throw new System.ArgumentNullException("lhs");
            if (rhs == null) throw new System.ArgumentNullException("rhs");

            IList<T> first = lhs as IList<T> ?? new List<T>(lhs);
            IList<T> second = rhs as IList<T> ?? new List<T>(rhs);

            int n = first.Count, m = second.Count;
            if (n == 0) return m;
            if (m == 0) return n;

            int curRow = 0, nextRow = 1;
            int[][] rows = { new int[m + 1], new int[m + 1] };
            for (int j = 0; j <= m; ++j)
                rows[curRow][j] = j;

            for (int i = 1; i <= n; ++i)
            {
                rows[nextRow][0] = i;

                for (int j = 1; j <= m; ++j)
                {
                    int dist1 = rows[curRow][j] + 1;
                    int dist2 = rows[nextRow][j - 1] + 1;
                    int dist3 = rows[curRow][j - 1] +
                        (first[i - 1].Equals(second[j - 1]) ? 0 : 1);

                    rows[nextRow][j] = System.Math.Min(dist1, System.Math.Min(dist2, dist3));
                }
                if (curRow == 0)
                {
                    curRow = 1;
                    nextRow = 0;
                }
                else
                {
                    curRow = 0;
                    nextRow = 1;
                }
            }
            return rows[curRow][m];
        }

        public static int LevenshteinDistance(string lhs, string rhs, bool caseSensitive = true)
        {
            if (!caseSensitive)
            {
                lhs = lhs.ToLower();
                rhs = rhs.ToLower();
            }
            char[] first = lhs.ToCharArray();
            char[] second = rhs.ToCharArray();
            return LevenshteinDistance(first, second);
        }

        internal static Texture2D GetThumbnailForGameObject(GameObject go)
        {
            var thumbnail = PrefabUtility.GetIconForGameObject(go);
            if (thumbnail)
                return thumbnail;
            return EditorGUIUtility.ObjectContent(go, go.GetType()).image as Texture2D;
        }

        private static MethodInfo s_FindTextureMethod;
        internal static Texture2D FindTextureForType(Type type)
        {
            if (s_FindTextureMethod == null)
            {
                var t = typeof(EditorGUIUtility);
                s_FindTextureMethod = t.GetMethod("FindTexture", BindingFlags.NonPublic| BindingFlags.Static);
            }
            return (Texture2D)s_FindTextureMethod.Invoke(null, new object[]{type});
        }

        private static MethodInfo s_GetIconForObject;
        internal static Texture2D GetIconForObject(UnityEngine.Object obj)
        {
            if (s_GetIconForObject == null)
            {
                var t = typeof(EditorGUIUtility);
                s_GetIconForObject = t.GetMethod("GetIconForObject", BindingFlags.NonPublic | BindingFlags.Static);
            }
            return (Texture2D)s_GetIconForObject.Invoke(null, new object[] { obj });
        }

        internal static void PingAsset(string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset != null)
                EditorGUIUtility.PingObject(asset);
        }

        #if UNITY_2020_1_OR_NEWER

        private static MethodInfo s_OpenPropertyEditorMethod;
        internal static EditorWindow OpenPropertyEditor(UnityEngine.Object obj)
        {
            if (s_OpenPropertyEditorMethod == null)
            {
                Assembly assembly = typeof(UnityEditor.EditorWindow).Assembly;
                var type = assembly.GetTypes().First(t => t.FullName == "UnityEditor.PropertyEditor");
                s_OpenPropertyEditorMethod = type.GetMethod("OpenPropertyEditor", BindingFlags.NonPublic | BindingFlags.Static);
                if (s_OpenPropertyEditorMethod == null)
                    return null;
            }
            return (EditorWindow)s_OpenPropertyEditorMethod.Invoke(null, new object[] {obj, true});
        }

        // TODO: Fix issue if PingUIElement is called more than once before delayCall is called, locking the window with the new style
        internal static void PingUIElement(VisualElement element, [CanBeNull] EditorWindow window)
        {
            var s = element.style;
            var oldBorderTopColor = s.borderTopColor;
            var oldBorderBottomColor = s.borderBottomColor;
            var oldBorderLeftColor = s.borderLeftColor;
            var oldBorderRightColor = s.borderRightColor;
            var oldBorderTopWidth = s.borderTopWidth;
            var oldBorderBottomWidth = s.borderBottomWidth;
            var oldBorderLeftWidth = s.borderLeftWidth;
            var oldBorderRightWidth = s.borderRightWidth;

            s.borderTopWidth = s.borderBottomWidth = s.borderLeftWidth = s.borderRightWidth = new StyleFloat(2);
            s.borderTopColor = s.borderBottomColor = s.borderLeftColor = s.borderRightColor = new StyleColor(Color.cyan);

            element.Focus();

            Utils.DelayCall(1f, () =>
            {
                s.borderTopColor = oldBorderTopColor;
                s.borderBottomColor = oldBorderBottomColor;
                s.borderLeftColor = oldBorderLeftColor;
                s.borderRightColor = oldBorderRightColor;
                s.borderTopWidth = oldBorderTopWidth;
                s.borderBottomWidth = oldBorderBottomWidth;
                s.borderLeftWidth = oldBorderLeftWidth;
                s.borderRightWidth = oldBorderRightWidth;

                if (window)
                    window.Repaint();
            });
        }
        #endif

        internal static void DelayCall(float seconds, System.Action callback)
        {
            DelayCall(EditorApplication.timeSinceStartup, seconds, callback);
        }

        internal static void DelayCall(double timeStart, float seconds, System.Action callback)
        {
            var dt = EditorApplication.timeSinceStartup - timeStart;
            if (dt >= seconds)
                callback();
            else
                EditorApplication.delayCall += () => DelayCall(timeStart, seconds, callback);
        }

        public static T ConvertValue<T>(string value)
        {
            var type = typeof(T);
            var converter = TypeDescriptor.GetConverter(type);
            if (converter.IsValid(value))
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                return (T)converter.ConvertFromString(null, CultureInfo.InvariantCulture, value);
            }
            return (T)Activator.CreateInstance(type);
        }

        public static bool TryConvertValue<T>(string value, out T convertedValue)
        {
            var type = typeof(T);
            var converter = TypeDescriptor.GetConverter(type);
            if (converter.IsValid(value))
            {
                // ReSharper disable once AssignNullToNotNullAttribute
                convertedValue = (T)converter.ConvertFromString(null, CultureInfo.InvariantCulture, value);
                return true;
            }

            convertedValue = default;
            return false;
        }

        private static UnityEngine.Object s_LastDraggedObject;
        internal static void StartDrag(UnityEngine.Object obj, string label = null)
        {
            s_LastDraggedObject = obj;
            if (!s_LastDraggedObject)
                return;
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.objectReferences = new[] { s_LastDraggedObject };
            DragAndDrop.StartDrag(label ?? s_LastDraggedObject.name);
        }

        public static bool IsRunningTests()
        {
            return !InternalEditorUtility.isHumanControllingUs || InternalEditorUtility.inBatchMode;
        }
    }

    internal struct DebugTimer : IDisposable
    {
        private bool m_Disposed;
        private string m_Name;
        private Stopwatch m_Timer;

        public double timeMs => m_Timer.Elapsed.TotalMilliseconds;

        public DebugTimer(string name)
        {
            m_Disposed = false;
            m_Name = name;
            m_Timer = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            if (m_Disposed)
                return;
            m_Disposed = true;
            m_Timer.Stop();
            #if UNITY_2019_1_OR_NEWER
            if (!String.IsNullOrEmpty(m_Name))
                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, $"{m_Name} took {timeMs:F2} ms");
            #else
            if (!String.IsNullOrEmpty(m_Name))
                Debug.Log($"{m_Name} took {timeMs} ms");
            #endif
        }
    }

    internal static class TryConvert
    {
        public static bool ToBool(string value, bool defaultValue = false)
        {
            try
            {
                return Convert.ToBoolean(value);
            }
            catch(Exception)
            {
                return defaultValue;
            }
        }

        public static float ToFloat(string value, float defaultValue = 0f)
        {
            try
            {
                return Convert.ToSingle(value);
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }

        public static int ToInt(string value, int defaultValue = 0)
        {
            try
            {
                return Convert.ToInt32(value);
            }
            catch (Exception)
            {
                return defaultValue;
            }
        }
    }

    #if UNITY_EDITOR
    [InitializeOnLoad]
    #endif
    internal static class UnityVersion
    {
        enum Candidate
        {
            Dev = 0,
            Alpha = 1 << 8,
            Beta = 1 << 16,
            Final = 1 << 24
        }

        static UnityVersion()
        {
            var version = Application.unityVersion.Split('.');

            if (version.Length < 2)
            {
                Console.WriteLine("Could not parse current Unity version '" + Application.unityVersion + "'; not enough version elements.");
                return;
            }

            if (int.TryParse(version[0], out Major) == false)
            {
                Console.WriteLine("Could not parse major part '" + version[0] + "' of Unity version '" + Application.unityVersion + "'.");
            }

            if (int.TryParse(version[1], out Minor) == false)
            {
                Console.WriteLine("Could not parse minor part '" + version[1] + "' of Unity version '" + Application.unityVersion + "'.");
            }

            if (version.Length >= 3)
            {
                try
                {
                    Build = ParseBuild(version[2]);
                }
                catch
                {
                    Console.WriteLine("Could not parse minor part '" + version[1] + "' of Unity version '" + Application.unityVersion + "'.");
                }
            }

            #if QUICKSEARCH_DEBUG
            Debug.Log($"Unity {Major}.{Minor}.{Build}");
            #endif
        }

        public static int ParseBuild(string build)
        {
            var rev = 0;
            if (build.Contains("a"))
                rev = (int)Candidate.Alpha;
            else if (build.Contains("b"))
                rev = (int)Candidate.Beta;
            if (build.Contains("f"))
                rev = (int)Candidate.Final;
            var tags = build.Split('a', 'b', 'f', 'p', 'x');
            if (tags.Length == 2)
            {
                rev += Convert.ToInt32(tags[0], 10) << 4;
                rev += Convert.ToInt32(tags[1], 10);
            }
            return rev;
        }

        [UsedImplicitly, RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureLoaded()
        {
            // This method ensures that this type has been initialized before any loading of objects occurs.
            // If this isn't done, the static constructor may be invoked at an illegal time that is not
            // allowed by Unity. During scene deserialization, off the main thread, is an example.
        }

        public static bool IsVersionGreaterOrEqual(int major, int minor)
        {
            if (Major > major)
                return true;
            if (Major == major)
            {
                if (Minor >= minor)
                    return true;
            }

            return false;
        }

        public static bool IsVersionGreaterOrEqual(int major, int minor, int build)
        {
            if (Major > major)
                return true;
            if (Major == major)
            {
                if (Minor > minor)
                    return true;

                if (Minor == minor)
                {
                    if (Build >= build)
                        return true;
                }
            }

            return false;
        }

        public static readonly int Major;
        public static readonly int Minor;
        public static readonly int Build;
    }

    internal struct BlinkCursorScope : IDisposable
    {
        private bool changed;
        private Color oldCursorColor;

        public BlinkCursorScope(bool blink, Color blinkColor)
        {
            changed = false;
            oldCursorColor = Color.white;
            if (blink)
            {
                oldCursorColor = GUI.skin.settings.cursorColor;
                GUI.skin.settings.cursorColor = blinkColor;
                changed = true;
            }
        }

        public void Dispose()
        {
            if (changed)
            {
                GUI.skin.settings.cursorColor = oldCursorColor;
            }
        }
    }
}
