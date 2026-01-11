namespace Match3.Core.Models.Enums
{
    /// <summary>
    /// Defines how a cover element behaves in relation to gravity and movement.
    /// </summary>
    public enum CoverAttachmentMode
    {
        /// <summary>
        /// The cover is attached to the grid cell. 
        /// It does not fall with gravity.
        /// Typically blocks items from falling into or out of this cell.
        /// Example: Ice, Cage, Wall.
        /// </summary>
        Static,

        /// <summary>
        /// The cover is attached to the unit element.
        /// It falls together with the unit.
        /// Example: Bubble, Chain (if implemented as falling), Vines.
        /// </summary>
        Dynamic
    }
}
