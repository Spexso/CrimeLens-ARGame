using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Database;
using Firebase.Extensions;
using System.Linq;
using System.Threading.Tasks;

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; }

    public FirebaseAuth Auth { get; private set; }
    public DatabaseReference DatabaseRef { get; private set; }
    public FirebaseUser CurrentUser => Auth?.CurrentUser;

    public bool IsInitialized { get; private set; }

    private string currentUsername;
    private string currentUserId;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            StartCoroutine(InitializeFirebase());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private IEnumerator InitializeFirebase()
    {
        Debug.Log("[Firebase] Initializing...");

        var dependencyTask = FirebaseApp.CheckAndFixDependenciesAsync();
        yield return new WaitUntil(() => dependencyTask.IsCompleted);

        if (dependencyTask.Result == DependencyStatus.Available)
        {
            var app = FirebaseApp.DefaultInstance;
            Auth = FirebaseAuth.DefaultInstance;
            DatabaseRef = FirebaseDatabase.DefaultInstance.RootReference;

            // Disable local caching for real=time database
            FirebaseDatabase.DefaultInstance.SetPersistenceEnabled(false);

            IsInitialized = true;

            Debug.Log("[Firebase] Initialized successfully!");
            Debug.Log($"[Firebase] Project ID: {app.Options.ProjectId}");
            Debug.Log($"[Firebase] App ID: {app.Options.AppId}");

            // Test database connection
            TestDatabaseConnection();
        }
        else
        {
            Debug.LogError($"[Firebase] Initialization failed: {dependencyTask.Result}");
            IsInitialized = false;
        }
    }

    private void TestDatabaseConnection()
    {
        Debug.Log("[Firebase] Testing database connection...");

        DatabaseRef.Child("test").SetValueAsync("Connection test from Unity!")
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted)
                {
                    Debug.Log("[Firebase] Database write successful!");
                }
                else
                {
                    Debug.LogError($"[Firebase]  Database write failed: {task.Exception}");
                }
            });
    }

    public void LoginWithUsername(string username, Action<string, string> onSuccess, Action<string> onError)
    {
        if (!IsInitialized || DatabaseRef == null)
        {
            onError?.Invoke("Database not initialized");
            return;
        }

        string usernameLower = username.ToLower();
        Debug.Log($"[Firebase] Looking up username: {usernameLower}");

        DatabaseRef.Child("usernames").Child(usernameLower)
            .GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError($"[Firebase] Username lookup failed: {task.Exception}");
                    onError?.Invoke("Failed to login");
                    return;
                }

                DataSnapshot snapshot = task.Result;

                if (snapshot.Exists)
                {
                    // Username exists - verify user data exists too
                    string userId = snapshot.Value.ToString();
                    Debug.Log($"[Firebase] Found username mapping: {usernameLower} → {userId}");

                    // Verify user actually exists in database
                    DatabaseRef.Child("users").Child(userId)
                        .GetValueAsync().ContinueWithOnMainThread(userTask =>
                        {
                            if (userTask.IsFaulted)
                            {
                                Debug.LogError($"[Firebase] User verification failed: {userTask.Exception}");
                                onError?.Invoke("Failed to load user");
                                return;
                            }

                            DataSnapshot userSnapshot = userTask.Result;

                            if (userSnapshot.Exists)
                            {
                                // User exists - load it
                                currentUsername = username;
                                currentUserId = userId;

                                Debug.Log($"[Firebase] Logged in: {username} ({userId})");
                                Debug.Log($"[Firebase] User stats: Solved={userSnapshot.Child("total_solved").Value}, " +
                                         $"Mysteries={userSnapshot.Child("total_mysteries").Value}");

                                onSuccess?.Invoke(username, userId);
                            }
                            else
                            {
                                // ❌ Username mapping exists but user data is missing
                                Debug.LogWarning($"[Firebase] Username exists but user data missing for {userId}");
                                Debug.LogWarning($"[Firebase] Recreating user profile...");

                                // Recreate the missing user profile
                                RecreateUserProfile(userId, username, onSuccess, onError);
                            }
                        });
                }
                else
                {
                    // Username doesn't exist - create new account
                    Debug.Log($"[Firebase] New username: {usernameLower}");
                    CreateNewAccountWithAuth(username, onSuccess, onError);
                }
            });
    }

    private void RecreateUserProfile(string userId, string username, Action<string, string> onSuccess, Action<string> onError)
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var userProfile = new Dictionary<string, object>
    {
        { "username", username },
        { "total_solved", 0 },
        { "total_mysteries", 0 },
        { "fastest_time", 0 },
        { "current_streak", 0 },
        { "created_at", timestamp }
    };

        DatabaseRef.Child("users").Child(userId)
            .SetValueAsync(userProfile)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError($"[Firebase] Failed to recreate user profile: {task.Exception}");
                    onError?.Invoke("Failed to create user profile");
                    return;
                }

                currentUsername = username;
                currentUserId = userId;

                Debug.Log($"[Firebase] User profile recreated: {username} ({userId})");
                onSuccess?.Invoke(username, userId);
            });
    }

    private void CreateNewAccountWithAuth(string username, Action<string, string> onSuccess, Action<string> onError)
    {
        if (Auth == null)
        {
            onError?.Invoke("Firebase Auth not initialized");
            return;
        }

        string usernameLower = username.ToLower();

        Debug.Log($"[Firebase] Creating NEW account for: {username}");

        // Sign out any existing session
        if (Auth.CurrentUser != null)
        {
            Debug.Log($"[Firebase] Signing out existing user: {Auth.CurrentUser.UserId}");
            Auth.SignOut();
        }

        // Create NEW Firebase anonymous user
        Auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(authTask =>
        {
            if (authTask.IsFaulted || authTask.IsCanceled)
            {
                Debug.LogError($"[Firebase] Auth failed: {authTask.Exception}");
                onError?.Invoke("Failed to create account");
                return;
            }

            string userId = authTask.Result.User.UserId;
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            Debug.Log($"[Firebase] Created NEW Firebase Auth user: {userId}");

            var updates = new Dictionary<string, object>
            {
            { $"usernames/{usernameLower}", userId },
            { $"users/{userId}/username", username },
            { $"users/{userId}/total_solved", 0 },
            { $"users/{userId}/total_mysteries", 0 },
            { $"users/{userId}/fastest_time", 0 },
            { $"users/{userId}/current_streak", 0 },
            { $"users/{userId}/created_at", timestamp }
            };

            Debug.Log($"[Firebase] Saving account to database...");

            DatabaseRef.UpdateChildrenAsync(updates).ContinueWithOnMainThread(updateTask =>
            {
                if (updateTask.IsFaulted)
                {
                    Debug.LogError($"[Firebase] Failed to save account: {updateTask.Exception}");
                    Debug.LogError($"[Firebase] Exception: {updateTask.Exception}");
                    onError?.Invoke("Failed to create account");
                    return;
                }

                currentUsername = username;
                currentUserId = userId;

                Debug.Log($"[Firebase] NEW account created: {username} ({userId})");
                onSuccess?.Invoke(username, userId);
            });
        });
    }

    public void SaveMysteryResult(bool isSolved, float completionTime)
    {
        if (!IsInitialized || string.IsNullOrEmpty(currentUserId))
        {
            Debug.LogWarning("[Firebase] Cannot save result - not signed in");
            return;
        }

        SaveMysteryResultAsync(isSolved, completionTime);
    }

    private void SaveMysteryResultAsync(bool isSolved, float completionTime)
    {
        string userId = currentUserId;
        completionTime = (int)Math.Ceiling(completionTime);
        Debug.Log($"[Firebase] Saving mystery result - Solved: {isSolved}, Time: {completionTime:F2}s");

        // Step 1: Create mystery record
        var mysteryRef = DatabaseRef.Child("mysteries").Push();
        var mysteryData = new Dictionary<string, object>
    {
        { "user_id", userId },
        { "is_solved", isSolved },
        { "completion_time", completionTime },
        { "timestamp", ServerValue.Timestamp }
    };

        mysteryRef.UpdateChildrenAsync(mysteryData).ContinueWithOnMainThread(mysteryTask =>
        {
            if (mysteryTask.IsFaulted)
            {
                Debug.LogError($"[Firebase] Failed to save mystery: {mysteryTask.Exception}");
                return;
            }

            Debug.Log($"[Firebase] Mystery record saved: {mysteryRef.Key}");

            // Step 2: Update user statistics using transaction to prevent race conditions
            UpdateUserStatsWithTransaction(userId, isSolved, completionTime);
        });
    }

    private void UpdateUserStatsWithTransaction(string userId, bool isSolved, float completionTime)
    {
        var userRef = DatabaseRef.Child("users").Child(userId);

        userRef.RunTransaction(mutableData =>
        {
            // Handle null data (first write or cached data unavailable)
            if (mutableData.Value == null)
            {
                // Initialize new user data
                var newUserData = new Dictionary<string, object>
                {
                { "username", currentUsername },
                { "total_mysteries", 1 },
                { "total_solved", isSolved ? 1 : 0 },
                { "fastest_time", isSolved ? completionTime : 0f },
                { "current_streak", isSolved ? 1 : 0 },
                { "created_at", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
                };

                mutableData.Value = newUserData;
                return TransactionResult.Success(mutableData);
            }

            // Get current values safely
            var userData = mutableData.Value as Dictionary<string, object>;
            if (userData == null)
            {
                Debug.LogError("[Firebase] Invalid user data format");
                return TransactionResult.Abort();
            }

            // Read current stats
            int totalMysteries = GetIntValue(userData, "total_mysteries", 0);
            int totalSolved = GetIntValue(userData, "total_solved", 0);
            float fastestTime = GetFloatValue(userData, "fastest_time", 0f);
            int currentStreak = GetIntValue(userData, "current_streak", 0);

            // Update stats
            totalMysteries++;

            if (isSolved)
            {
                totalSolved++;
                currentStreak++;

                // Update fastest time (only if solved and better than current)
                if (fastestTime == 0 || completionTime < fastestTime)
                {
                    fastestTime = completionTime;
                }
            }
            else
            {
                // Reset streak on failure
                currentStreak = 0;
            }

            // Write updated values back
            userData["total_mysteries"] = totalMysteries;
            userData["total_solved"] = totalSolved;
            userData["fastest_time"] = fastestTime;
            userData["current_streak"] = currentStreak;

            mutableData.Value = userData;
            return TransactionResult.Success(mutableData);

        }).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError($"[Firebase] Transaction failed: {task.Exception}");
                return;
            }

            if (task.IsCompleted && !task.IsCanceled)
            {
                var snapshot = task.Result;
                Debug.Log($"[Firebase] Stats updated successfully");
                Debug.Log($"[Firebase] Total: {GetIntValue(snapshot, "total_mysteries", 0)}, " +
                         $"Solved: {GetIntValue(snapshot, "total_solved", 0)}, " +
                         $"Fastest: {GetFloatValue(snapshot, "fastest_time", 0f):F2}s");
            }
        });
    }

    private int GetIntValue(Dictionary<string, object> data, string key, int defaultValue)
    {
        if (data.TryGetValue(key, out object value))
        {
            if (value is long longValue)
                return (int)longValue;
            if (value is int intValue)
                return intValue;
            if (value is string strValue && int.TryParse(strValue, out int parsedValue))
                return parsedValue;
        }
        return defaultValue;
    }

    private int GetIntValue(DataSnapshot snapshot, string key, int defaultValue)
    {
        if (snapshot.HasChild(key))
        {
            var child = snapshot.Child(key);
            if (child.Value is long longValue)
                return (int)longValue;
            if (child.Value is int intValue)
                return intValue;
        }
        return defaultValue;
    }

    private float GetFloatValue(Dictionary<string, object> data, string key, float defaultValue)
    {
        if (data.TryGetValue(key, out object value))
        {
            if (value is double doubleValue)
                return (float)doubleValue;
            if (value is float floatValue)
                return floatValue;
            if (value is long longValue)
                return (float)longValue;
            if (value is int intValue)
                return (float)intValue;
            if (value is string strValue && float.TryParse(strValue, out float parsedValue))
                return parsedValue;
        }
        return defaultValue;
    }

    private float GetFloatValue(DataSnapshot snapshot, string key, float defaultValue)
    {
        if (snapshot.HasChild(key))
        {
            var child = snapshot.Child(key);
            if (child.Value is double doubleValue)
                return (float)doubleValue;
            if (child.Value is float floatValue)
                return floatValue;
            if (child.Value is long longValue)
                return (float)longValue;
            if (child.Value is int intValue)
                return (float)intValue;
        }
        return defaultValue;
    }

    public void GetLeaderboard(Action<List<LeaderboardEntry>> onSuccess, Action<string> onError)
    {
        if (!IsInitialized || DatabaseRef == null)
        {
            onError?.Invoke("Database not initialized");
            return;
        }

        Debug.Log("[Firebase] Starting leaderboard query...");

        var usersRef = DatabaseRef.Child("users");

        // Force disable sync, then re-enable to force fresh fetch
        usersRef.KeepSynced(false);
        usersRef.KeepSynced(true);

        usersRef.OrderByChild("total_solved")
            .LimitToLast(10)
            .GetValueAsync()
            .ContinueWithOnMainThread(task =>
            {
                Debug.Log("[Firebase] Query callback triggered");

                if (task.IsFaulted)
                {
                    Debug.LogError($"[Firebase] Leaderboard query failed: {task.Exception}");
                    onError?.Invoke("Failed to load leaderboard");
                    return;
                }

                Debug.Log("[Firebase] Query completed");

                var leaderboard = new List<LeaderboardEntry>();
                DataSnapshot snapshot = task.Result;

                Debug.Log($"[Firebase] Snapshot exists: {snapshot.Exists}");
                Debug.Log($"[Firebase] Snapshot has value: {snapshot.Value != null}");
                Debug.Log($"[Firebase] Snapshot children count: {snapshot.ChildrenCount}");

                if (snapshot.Exists && snapshot.ChildrenCount > 0)
                {
                    foreach (DataSnapshot userSnapshot in snapshot.Children)
                    {
                        string userId = userSnapshot.Key;
                        Debug.Log($"[Firebase] Processing user: {userId}");

                        try
                        {
                            if (!userSnapshot.HasChild("username"))
                            {
                                Debug.LogWarning($"[Firebase] Skipping user {userId} - no username field");
                                continue;
                            }

                            if (!userSnapshot.HasChild("total_solved"))
                            {
                                Debug.LogWarning($"[Firebase] Skipping user {userId} - no total_solved field");
                                continue;
                            }

                            var usernameRaw = userSnapshot.Child("username").Value;
                            var totalSolvedRaw = userSnapshot.Child("total_solved").Value;

                            Debug.Log($"[Firebase]   username raw: {usernameRaw}");
                            Debug.Log($"[Firebase]   total_solved raw: {totalSolvedRaw}");

                            if (usernameRaw == null || string.IsNullOrEmpty(usernameRaw.ToString()))
                            {
                                Debug.LogWarning($"[Firebase] Skipping user {userId} - empty username");
                                continue;
                            }

                            string username = usernameRaw.ToString();
                            int totalSolved = GetIntValue(userSnapshot, "total_solved", 0);

                            Debug.Log($"[Firebase] Parsed - username: {username}, totalSolved: {totalSolved}");

                            var entry = new LeaderboardEntry
                            {
                                username = username,
                                totalSolved = totalSolved,
                                totalMysteries = GetIntValue(userSnapshot, "total_mysteries", 0),
                                fastestTime = (int)GetFloatValue(userSnapshot, "fastest_time", 0),
                                currentStreak = GetIntValue(userSnapshot, "current_streak", 0)
                            };

                            leaderboard.Add(entry);
                            Debug.Log("[Firebase] Successfully added to leaderboard");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[Firebase] Error processing user {userId}: {ex.Message}");
                            continue;
                        }
                    }

                    Debug.Log($"[Firebase] Total VALID entries after filtering: {leaderboard.Count}");

                    // Sort by total_solved descending (highest first)
                    leaderboard = leaderboard
                        .OrderByDescending(e => e.totalSolved)
                        .ThenByDescending(e => e.currentStreak)
                        .ThenBy(e => e.fastestTime)
                        .ToList();

                    for (int i = 0; i < leaderboard.Count; i++)
                    {
                        leaderboard[i].rank = i + 1;  // Rank 1, 2, 3, ...
                    }

                    Debug.Log($"[Firebase] Successfully fetched leaderboard: {leaderboard.Count} entries");

                    for (int i = 0; i < leaderboard.Count; i++)
                    {
                        Debug.Log($"[Firebase]   #{i + 1}: {leaderboard[i].username} - {leaderboard[i].totalSolved} solved");
                    }

                    onSuccess?.Invoke(leaderboard);
                }
                else
                {
                    Debug.LogWarning("[Firebase] No leaderboard data found");
                    onSuccess?.Invoke(leaderboard);
                }
            });
    }

    public string GetCurrentUserId()
    {
        return currentUserId;
    }

    public string GetCurrentUsername()
    {
        return currentUsername;
    }

    public void SignOut()
    {
        if (Auth != null)
        {
            Auth.SignOut();
            currentUsername = null;
            currentUserId = null;
            Debug.Log("[Firebase] Signed out");
        }
    }
}