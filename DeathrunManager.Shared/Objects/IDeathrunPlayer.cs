using DeathrunManager.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.GameObjects;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace DeathrunManager.Shared.Objects;

public interface IDeathrunPlayer
{
    #region DeathrunPlayer
    
    /// <summary>
    /// Gets the game client associated with the player. This client provides essential
    /// functionality for interacting with the game server, managing player sessions,
    /// and facilitating communication between the client and server components.
    /// </summary>
    IGameClient Client { get; }

    /// <summary>
    /// Gets the player controller associated with this player. This controller is responsible for
    /// managing and facilitating player-specific interactions, including movement, actions, team switching,
    /// and other gameplay features during the Deathrun game mode.
    /// </summary>
    IPlayerController? Controller { get; }

    /// <summary>
    /// 
    /// </summary>
    IPlayerPawn? PlayerPawn { get; }

    /// <summary>
    /// Gets the observer service associated with the player's pawn. This service provides functionality
    /// for managing the observer state and behavior, allowing interactions specific to the observer role
    /// during gameplay.
    /// </summary>
    IObserverService? ObserverPawnService { get; }

    /// <summary>
    /// Gets the game client currently being observed by the player.
    /// This client represents another player's perspective, enabling functionalities
    /// such as spectating during gameplay or observing actions for strategic purposes.
    /// </summary>
    IDeathrunPlayer? ObservedDeathrunPlayer { get; }
    
    /// <summary>
    /// Gets or sets the player's class in the Deathrun game, which determines their current role.
    /// The player's class can either be "Contestant" or "Game Master". The chosen class influences
    /// gameplay behavior and responsibilities within the game.
    /// </summary>
    DPlayerClass Class { get; set; }

    /// <summary>
    /// Initializes the lives system for the player.
    /// This method creates a new instance of the lives system associated with the player
    /// and ensures that it is properly set up and functional for use during gameplay.
    /// </summary>
    /// <returns>Returns true if the lives system is successfully initialized; otherwise, false.</returns>
    bool InitLivesSystem();

    /// <summary>
    /// Represents the lives management system associated with the player in the Deathrun context.
    /// This property provides access to functionality that allows tracking and modification of the
    /// player's remaining lives, such as adding, removing, or resetting lives; and respawning
    /// mechanics tied to life usage.
    /// </summary>
    ILivesSystem? LivesSystem { get; }

    /// <summary>
    /// Initializes the economy system for the player.
    /// This method sets up a new instance of the economy system associated with the player,
    /// ensuring it is properly configured and ready for use during gameplay.
    /// </summary>
    /// <returns>Returns true if the economy system is successfully initialized; otherwise, false.</returns>
    bool InitEconomySystem();

    /// <summary>
    /// Represents the economy system associated with the player.
    /// This system manages the player's credits, providing functionality to
    /// add, deduct, or reset credits, as well as generate visual representations
    /// of the player's current credit balance.
    /// </summary>
    IEconomySystem? EconomySystem { get; }

    /// <summary>
    /// Gets a value indicating whether the player's controller and player pawn are valid entities and the player is currently connected.
    /// The property returns true if all related components required to represent a functioning player are in a valid state.
    /// Otherwise, it returns false.
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    /// Gets a value indicating whether the player is in a valid state and their player pawn is alive.
    /// The property returns true if all required components for a functioning player are present and properly initialized,
    /// and the player pawn is alive. Otherwise, it returns false.
    /// </summary>
    bool IsValidAndAlive { get; }
    
    /// <summary>
    /// Gets or sets a value indicating whether the player should be excluded from being selected as the Game Master in the next Game Master selection process.
    /// If set to true, the player will be skipped for the upcoming selection and the value will reset to false afterward.
    /// </summary>
    bool SkipNextGameMasterPickUp { get; set; }

    /// <summary>
    /// Indicates whether the Deathrun HUD should be rendered for the player.
    /// This property determines the visibility of the on-screen interface elements
    /// specific to the Deathrun game mode, such as timers, scores, or player status,
    /// enhancing the player's interaction within the game.
    /// </summary>
    bool RenderDeathrunHud { get; set; }

    #endregion
    
    #region Change Class Method

    /// <summary>
    /// Changes the class of the player to the specified new class.
    /// This method allows switching between Contestant and GameMaster roles
    /// and handles necessary internal changes depending on the selected class.
    /// If forced, the GameMaster role swaps with the Contestant role under specific conditions.
    /// </summary>
    /// <param name="newClass">The new player class to switch to. Possible values are Contestant or GameMaster.</param>
    /// <param name="force">Indicates whether the change should forcibly swap roles between game master and contestant.</param>
    void ChangeClass(DPlayerClass newClass, bool force = false);

    #endregion
    
    #region Chat

    /// <summary>
    /// Sends a chat message to the player with an optional prefix.
    /// This method formats the chat message based on configuration settings and sends it to the recipient(s).
    /// </summary>
    /// <param name="message">The content of the chat message to be sent. If null or empty, the message is ignored.</param>
    /// <param name="addPrefix">Determines whether a predefined prefix is added to the message. Defaults to true.</param>
    void SendChatMessage(string message, bool addPrefix = true);

    /// <summary>
    /// Sends a chat message to players with an optional prefix.
    /// This method formats the message, optionally adds a predefined prefix, and sends it through the game's chat system.
    /// </summary>
    /// <param name="message">The chat message to be sent.</param>
    /// <param name="recipientFilter">Specifies the recipients of the chat message.</param>
    /// <param name="addPrefix">Determines whether to include the predefined prefix in the message.</param>
    void SendChatMessage(string message, RecipientFilter recipientFilter, bool addPrefix = true);

    #endregion

    #region Html Center Menu

    /// <summary>
    /// Displays a formatted HTML message at the center of the player's screen.
    /// This method ensures the message is delivered to a valid client and populates
    /// an event with the necessary data to temporarily display the message.
    /// </summary>
    /// <param name="message">The HTML message to display. The message must not be null or empty.</param>
    void PrintToCenterHtml(string message);

    /// <summary>
    /// Sets the HTML content for the first cell in the top row of the center menu.
    /// This method is used to customize the display of the center menu by assigning
    /// specific HTML content to the first cell in its top row.
    /// </summary>
    /// <param name="htmlString">The HTML string to set for the first cell in the top row.
    /// It can be null to clear the content of the cell.</param>
    public void SetCenterMenuTopRowCellOneHtml(string? htmlString);

    /// <summary>
    /// Sets the HTML content for the second cell in the top row of the center menu.
    /// This method updates the content of the specified cell to the provided HTML string.
    /// </summary>
    /// <param name="htmlString">The HTML string to set as the content of the second cell. Can be null to clear the content.</param>
    public void SetCenterMenuTopRowCellTwoHtml(string? htmlString);

    /// <summary>
    /// Sets the HTML content for the third cell in the top row of the center menu.
    /// This method allows for customization of the center menu's top row appearance by defining the HTML content displayed in the third cell.
    /// </summary>
    /// <param name="htmlString">The HTML string to set for the third cell. Can be null to clear the content of the cell.</param>
    public void SetCenterMenuTopRowCellThreeHtml(string? htmlString);

    /// <summary>
    /// Updates the HTML content for the fourth cell in the top row of the center menu.
    /// This method sets the specified HTML string to be displayed in the designated
    /// cell, allowing dynamic updates of menu content during gameplay.
    /// </summary>
    /// <param name="htmlString">The HTML content to set for the fourth cell in the top row of the center menu.
    /// If null, the cell will be cleared.</param>
    public void SetCenterMenuTopRowCellFourHtml(string? htmlString);

    /// <summary>
    /// Sets the HTML content for the middle row of the center menu.
    /// This content is displayed to the player as part of the UI.
    /// </summary>
    /// <param name="htmlString">The HTML string to set for the middle row. Can be null to clear the content.</param>
    void SetCenterMenuMiddleRowHtml(string? htmlString);

    /// <summary>
    /// Updates the HTML content of the bottom row in the center menu.
    /// This method sets the specified HTML string to the bottom row of the center menu,
    /// allowing dynamic customization or updates to its content.
    /// </summary>
    /// <param name="htmlString">The HTML content to set for the bottom row of the center menu. Pass null to clear the content.</param>
    void SetCenterMenuBottomRowHtml(string? htmlString);

    #endregion
}