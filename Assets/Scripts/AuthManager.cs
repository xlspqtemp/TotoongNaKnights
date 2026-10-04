using LootLocker.Requests;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Handles LootLocker guest, white-label login, and account creation requests for the login screen.
/// </summary>
public class AuthManager : MonoBehaviour
{
    private const int MinimumPasswordLength = 8;
    private const string GmailAddressErrorMessage = "Please use a valid Gmail address (example@gmail.com).";
    private const string LoginFailedMessage = "Login failed. Check your email and password.";
    private const string ServerUnavailableMessage = "Can't reach the server. Check your internet connection.";
    private const string SignUpGenericFailureMessage = "Sign up couldn't be completed. Please try again.";
    private const string LoginGenericFailureMessage = "Login couldn't be completed. Please try again.";
    private const string MainMenuSceneNameFallback = "MainMenu";

    [SerializeField] private TMP_InputField emailInputField;
    [SerializeField] private TMP_InputField passwordInputField;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button signUpButton;
    [SerializeField] private Button guestButton;
    [SerializeField] private Button backButton;
    [SerializeField] private string mainMenuSceneName = MainMenuSceneNameFallback;

    private bool requestInProgress;

    /// <summary>
    /// Connects the generated login UI to this authentication manager.
    /// </summary>
    public void Configure(TMP_InputField emailField, TMP_InputField passwordField, TextMeshProUGUI messageText,
        Button login, Button signUp, Button guest, Button back, string destinationScene)
    {
        emailInputField = emailField;
        passwordInputField = passwordField;
        statusText = messageText;
        loginButton = login;
        signUpButton = signUp;
        guestButton = guest;
        backButton = back;
        mainMenuSceneName = string.IsNullOrWhiteSpace(destinationScene)
            ? MainMenuSceneNameFallback
            : destinationScene;
    }

    /// <summary>
    /// Creates a LootLocker white-label account, then immediately starts a session for it.
    /// </summary>
    public void OnSignUpClicked()
    {
        if (requestInProgress)
        {
            return;
        }

        string rawEmail = emailInputField != null ? emailInputField.text : string.Empty;
        string password = passwordInputField != null ? passwordInputField.text : string.Empty;
        if (string.IsNullOrWhiteSpace(rawEmail) || string.IsNullOrWhiteSpace(password))
        {
            SetStatus("Enter your email address and password.");
            return;
        }

        if (!TryNormalizeGmailAddress(rawEmail, out string email))
        {
            SetStatus(GmailAddressErrorMessage);
            return;
        }

        if (password.Length < MinimumPasswordLength)
        {
            SetStatus("Use a password with at least 8 characters.");
            return;
        }

        BeginRequest("Creating account...");
        LootLockerSDKManager.WhiteLabelSignUp(email, password, response =>
        {
            if (this == null)
            {
                return;
            }

            if (response == null || !response.success)
            {
                string errorCode = response != null && response.errorData != null
                    ? response.errorData.code
                    : string.Empty;
                string errorMessage = response != null && response.errorData != null
                    ? response.errorData.message
                    : string.Empty;
                string failureMessage = GetSignUpFailureMessage(
                    response != null ? response.statusCode : 0, errorCode, errorMessage);
                FinishRequest(failureMessage);
                return;
            }

            SetStatus("Account created. Logging in...");
            StartWhiteLabelLogin(email, password);
        });
    }

    /// <summary>
    /// Logs in an existing white-label account and opens the main menu on success.
    /// </summary>
    public void OnLoginClicked()
    {
        if (requestInProgress)
        {
            return;
        }

        string rawEmail = emailInputField != null ? emailInputField.text : string.Empty;
        string password = passwordInputField != null ? passwordInputField.text : string.Empty;
        if (string.IsNullOrWhiteSpace(rawEmail) || string.IsNullOrWhiteSpace(password))
        {
            SetStatus("Enter your email address and password.");
            return;
        }

        if (!TryNormalizeGmailAddress(rawEmail, out string email))
        {
            SetStatus(GmailAddressErrorMessage);
            return;
        }

        BeginRequest("Logging in...");
        StartWhiteLabelLogin(email, password);
    }

    /// <summary>
    /// Starts a LootLocker guest session and opens the main menu on success.
    /// </summary>
    public void OnGuestClicked()
    {
        if (requestInProgress)
        {
            return;
        }

        BeginRequest("Starting guest session...");
        LootLockerSDKManager.StartGuestSession(response =>
        {
            if (this == null)
            {
                return;
            }

            if (!response.success)
            {
                FinishRequest("Guest login failed. Please try again.");
                return;
            }

            FinishAndLoadMainMenu("Guest session started.");
        });
    }

    private void StartWhiteLabelLogin(string email, string password)
    {
        LootLockerSDKManager.WhiteLabelLoginAndStartSession(email, password, true, response =>
        {
            if (this == null)
            {
                return;
            }

            if (response == null || !response.success)
            {
                FinishRequest(GetLoginFailureMessage(response));
                return;
            }

            LeaderboardManager.EnsureWhiteLabelDisplayName(email,
                response.SessionResponse != null ? response.SessionResponse.player_ulid : string.Empty,
                response.SessionResponse != null ? response.SessionResponse.player_name : string.Empty);
            FinishAndLoadMainMenu("Login successful.");
        });
    }

    private static bool TryNormalizeGmailAddress(string email, out string normalizedEmail)
    {
        normalizedEmail = string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().ToLowerInvariant();

        const string gmailSuffix = "@gmail.com";
        if (!normalizedEmail.EndsWith(gmailSuffix, System.StringComparison.Ordinal))
        {
            return false;
        }

        int atIndex = normalizedEmail.IndexOf('@');
        if (atIndex <= 0 || atIndex != normalizedEmail.LastIndexOf('@') ||
            atIndex != normalizedEmail.Length - gmailSuffix.Length)
        {
            return false;
        }

        for (int i = 0; i < atIndex; i++)
        {
            char character = normalizedEmail[i];
            bool allowed = (character >= 'a' && character <= 'z') ||
                           (character >= '0' && character <= '9') ||
                           character == '.' || character == '_' || character == '+' || character == '-';
            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    private static string GetSignUpFailureMessage(int statusCode, string errorCode, string errorMessage)
    {
        if (IsServerUnavailable(statusCode, errorCode, errorMessage))
        {
            return ServerUnavailableMessage;
        }

        if (IsEmailAlreadyRegistered(errorCode, errorMessage))
        {
            return "This email is already registered. Try logging in instead.";
        }

        return SignUpGenericFailureMessage;
    }

    private static string GetLoginFailureMessage(LootLockerWhiteLabelLoginAndStartSessionResponse response)
    {
        if (response == null)
        {
            return LoginGenericFailureMessage;
        }

        if (response.LoginResponse != null && !response.LoginResponse.success)
        {
            string loginErrorCode = response.LoginResponse.errorData != null
                ? response.LoginResponse.errorData.code
                : string.Empty;
            string loginErrorMessage = response.LoginResponse.errorData != null
                ? response.LoginResponse.errorData.message
                : string.Empty;

            if (IsServerUnavailable(response.LoginResponse.statusCode, loginErrorCode, loginErrorMessage))
            {
                return ServerUnavailableMessage;
            }

            if (IsInvalidCredentials(response.LoginResponse.statusCode, loginErrorCode, loginErrorMessage))
            {
                return LoginFailedMessage;
            }

            return LoginGenericFailureMessage;
        }

        if (response.SessionResponse != null && !response.SessionResponse.success &&
            IsServerUnavailable(response.SessionResponse.statusCode,
                response.SessionResponse.errorData != null ? response.SessionResponse.errorData.code : string.Empty,
                response.SessionResponse.errorData != null ? response.SessionResponse.errorData.message : string.Empty))
        {
            return ServerUnavailableMessage;
        }

        string errorCode = response.errorData != null ? response.errorData.code : string.Empty;
        string errorMessage = response.errorData != null ? response.errorData.message : string.Empty;
        return IsServerUnavailable(response.statusCode, errorCode, errorMessage)
            ? ServerUnavailableMessage
            : LoginGenericFailureMessage;
    }

    private static bool IsServerUnavailable(int statusCode, string errorCode, string errorMessage)
    {
        string normalizedCode = errorCode != null ? errorCode.Trim() : string.Empty;
        if (statusCode == 0 && normalizedCode.Equals("HTTP0", System.StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string message = errorMessage != null ? errorMessage.ToLowerInvariant() : string.Empty;
        return message.Contains("check your internet connection") ||
               message.Contains("network error") ||
               message.Contains("connection error") ||
               message.Contains("could not connect") ||
               message.Contains("unable to connect") ||
               message.Contains("failed to connect") ||
               message.Contains("could not be reached");
    }

    private static bool IsEmailAlreadyRegistered(string errorCode, string errorMessage)
    {
        string code = errorCode != null ? errorCode.ToLowerInvariant() : string.Empty;
        string message = errorMessage != null ? errorMessage.ToLowerInvariant() : string.Empty;
        return ContainsAny(code, "email_already_exists", "email_already_registered", "email_exists", "email_taken",
                   "user_already_exists", "user_exists") ||
               ContainsAny(message, "already registered", "already exists", "already taken", "email already",
                   "email has been taken", "email is taken", "duplicate email", "account with this email already exists");
    }

    private static bool IsInvalidCredentials(int statusCode, string errorCode, string errorMessage)
    {
        if (statusCode == 401)
        {
            return true;
        }

        string code = errorCode != null ? errorCode.ToLowerInvariant() : string.Empty;
        string message = errorMessage != null ? errorMessage.ToLowerInvariant() : string.Empty;
        return ContainsAny(code, "invalid_credentials", "invalid_email_or_password", "incorrect_credentials") ||
               ContainsAny(message, "invalid credentials", "invalid login credentials", "invalid email or password",
                   "incorrect email or password", "email or password is incorrect", "wrong email or password",
                   "email not found", "user not found", "player does not exist", "incorrect password", "wrong password");
    }

    private static bool ContainsAny(string value, params string[] terms)
    {
        for (int i = 0; i < terms.Length; i++)
        {
            if (value.Contains(terms[i]))
            {
                return true;
            }
        }

        return false;
    }

    private void BeginRequest(string message)
    {
        requestInProgress = true;
        SetButtonsInteractable(false);
        SetStatus(message);
    }

    private void FinishRequest(string message)
    {
        requestInProgress = false;
        SetButtonsInteractable(true);
        SetStatus(message);
    }

    private void FinishAndLoadMainMenu(string message)
    {
        requestInProgress = false;
        SetButtonsInteractable(true);
        SetStatus(message);
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (loginButton != null)
        {
            loginButton.interactable = interactable;
        }

        if (signUpButton != null)
        {
            signUpButton.interactable = interactable;
        }

        if (guestButton != null)
        {
            guestButton.interactable = interactable;
        }

        if (backButton != null)
        {
            backButton.interactable = interactable;
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}
