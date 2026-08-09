namespace MagicAssistant.Data.Model.Enums;

public enum LayoutEnum
{
    /// <summary>
    /// A standard Magic card with one face
    /// </summary>
    normal,
    
    /// <summary>
    /// A split -faced card
    /// </summary>
    split,

    /// <summary>
    /// Cards that invert vertically with the flip keyword
    /// </summary>
    flip,

    /// <summary>
    /// Double-sided cards that transform
    /// </summary>
    transform,

    /// <summary>
    /// Double-sided cards that can be played either - side
    /// </summary>
    modal_dfc,

    /// <summary>
    /// Cards with meld parts printed on the back
    /// </summary>
    meld,

    /// <summary>
    /// Cards with Level Up
    /// </summary>
    leveler,

    /// <summary>
    /// Class-type enchantment cards
    /// </summary>
    @class,

    /// <summary>
    /// Case-type enchantment cards
    /// </summary>
    @case,
	
    /// <summary>
    /// Saga - type cards
    /// </summary>
    saga,

    /// <summary>
    /// Cards with an Adventure spell part
    /// </summary>
    adventure,

    /// <summary>
    /// Cards with a prepared spell part
    /// </summary>
    prepare,

    /// <summary>
    /// Cards with Mutate
    /// </summary>
    mutate,

    /// <summary>
    /// Cards with Prototype
    /// </summary>
    prototype,

    /// <summary>
    /// Battle - type cards
    /// </summary>
    battle,

    /// <summary>
    /// Plane and Phenomenon - type cards
    /// </summary>
    planar,

    /// <summary>
    /// Scheme - type cards
    /// </summary>
    scheme,

    /// <summary>
    /// Vanguard - type cards
    /// </summary>
    vanguard,

    /// <summary>
    /// Token cards
    /// </summary>
    token,

    /// <summary>
    /// Tokens with another token printed on the back
    /// </summary>
    double_faced_token,

    /// <summary>
    /// Emblem cards
    /// </summary>
    emblem,

    /// <summary>
    /// Cards with Augment
    /// </summary>
    augment,

    /// <summary>
    /// Host-type cards
    /// </summary>
    host,

    /// <summary>
    /// Art Series collectable double-faced cards
    /// </summary>
    art_series,

    /// <summary>
    /// A Magic card with two sides that are unrelated
    /// </summary>
    reversible_card,
}
