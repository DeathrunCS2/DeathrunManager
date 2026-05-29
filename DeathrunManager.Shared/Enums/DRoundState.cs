namespace DeathrunManager.Shared.Enums;

/// <summary>
/// Represents the possible states of a deathrun round in the game.
/// </summary>
public enum DRoundState
{
    /// <summary>
    /// Represents an uninitialized or undefined state for a deathrun round.
    /// </summary>
    /// <remarks>
    /// The <c>Unset</c> value indicates that the state of the round has not yet been determined or initialized.
    /// It is typically used as a default value before any specific state is assigned.
    /// </remarks>
    Unset,

    /// <summary>
    /// Represents the pre-start state of a deathrun round.
    /// </summary>
    /// <remarks>
    /// The <c>StartPre</c> value indicates that the round is preparing to begin.
    /// This state is typically used for initialization or preparation tasks prior to the actual start of the round.
    /// </remarks>
    StartPre,
    /// <summary>
    /// Represents the state where the plugin is verifying whether all game-mode-bound requirements are met to be able to
    /// start the game mode.
    /// </summary>
    /// <remarks>
    /// This state is intended for the plugin to validate specific conditions to be able to
    /// start the game mode.
    /// </remarks>
    CheckGameModeRequirements,
    /// <summary>
    /// Represents the state in which a game master is being selected for the deathrun round.
    /// </summary>
    /// <remarks>
    /// The <c>PickingGameMaster</c> state occurs when the system is determining or assigning the game master role for the current round.
    /// This is an intermediary stage during the round setup phase.
    /// </remarks>
    PickingGameMaster,
    /// <summary>
    /// Indicates that a game master has been successfully selected for the deathrun round.
    /// </summary>
    /// <remarks>
    /// The <c>PickedGameMaster</c> state represents the point in the round's lifecycle
    /// where the process of selecting a game master has been completed. This state
    /// typically precedes any game-related actions or events initiated by the game master.
    /// </remarks>
    PickedGameMaster,
    /// <summary>
    /// Represents the state of the deathrun round immediately after completing the initialization phase.
    /// </summary>
    /// <remarks>
    /// The <c>StartPost</c> value indicates that the round has entered a transitional or early active phase,
    /// following the setup processes required prior to gameplay beginning.
    /// It typically succeeds the <c>StartPre</c> phase and precedes full engagement or next specific round states.
    /// </remarks>
    StartPost,
    
    /// <summary>
    /// Represents the state during the pre-conclusion phase of a deathrun round.
    /// </summary>
    /// <remarks>
    /// The <c>EndPre</c> value indicates that the round is in the initial stage of its ending phase,
    /// where transitional or preparatory actions for concluding the round are performed.
    /// </remarks>
    EndPre,
    /// <summary>
    /// Represents the finalizing stage after a deathrun round has concluded.
    /// </summary>
    /// <remarks>
    /// The <c>EndPost</c> value signifies the phase where all post-round actions or clean-up processes are handled.
    /// This state occurs after the main round activities and results have been processed.
    /// </remarks>
    EndPost
}