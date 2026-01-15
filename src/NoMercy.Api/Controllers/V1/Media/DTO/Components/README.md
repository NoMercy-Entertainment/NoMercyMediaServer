# Unified Component DTO System

This directory contains a unified component-based DTO structure for building API responses
that map directly to UI components on the Android/client side.

## Architecture

### Component Types

**Container Components** (can hold child components):
- `NMGrid` - Displays items in a grid layout
- `NMList` - Displays items in a vertical list
- `NMCarousel` - Displays items in a horizontal scrollable carousel
- `NMContainer` - Generic container for grouping components

**Leaf Components** (end nodes, cannot have children):
- `NMCard` - Standard media card
- `NMHomeCard` - Featured home page card with video support
- `NMGenreCard` - Genre category card
- `NMMusicCard` - Music album/artist card
- `NMMusicHomeCard` - Music home featured card
- `NMTrackRow` - Single track in a list
- `NMTopResultCard` - Search top result

### Nesting Rules

- Container → Container ✓ (Grid can contain Carousels)
- Container → Leaf ✓ (Carousel can contain Cards)
- Leaf → anything ✗ (Cards cannot have children)

## Usage Examples

### Simple Card

```csharp
using NoMercy.Api.Controllers.V1.Media.DTO.Components;

// Create a single card
ComponentEnvelope card = Component.Card(new CardData(movie, country))
    .WithWatch()
    ;
```

### Carousel with Cards

```csharp
// Create a carousel of movie cards
ComponentEnvelope carousel = Component.Carousel()
    .WithId("recent-movies")
    .WithTitle("Recent Movies")
    .WithMoreLink("/movies")
    .WithItems(movies.Select(m =>
        Component.Card(new CardData(m, country))
            .WithWatch()
            ))
    ;
```

### Nested Grid with Carousels

```csharp
// Create a grid containing multiple carousels
ComponentEnvelope grid = Component.Grid()
    .WithTitle("Browse")
    .WithItems(
        Component.Carousel()
            .WithTitle("Action")
            .WithItems(actionMovies.ToCards(country))
            ,
        Component.Carousel()
            .WithTitle("Comedy")
            .WithItems(comedyMovies.ToCards(country))
            
    )
    ;
```

### Using Extension Methods

```csharp
using NoMercy.Api.Controllers.V1.Media.DTO.Components;

// Convert domain models to components directly
IEnumerable<ComponentEnvelope> cards = movies.ToCards(country, watch: true);

// Wrap in container
ComponentEnvelope carousel = cards.WrapInCarousel("Latest Movies", "/movies/all");
```

### Building API Responses

```csharp
// Simple response with single component
return ComponentResponse.From(
    Component.HomeCard(new HomeCardData(movie, country))
);

// Response with multiple components
return ComponentResponse.From(
    homeCard,
    continueWatchingCarousel,
    ...genreCarousels
);
```

### With Update Configuration

```csharp
// Component that refreshes on page load
ComponentEnvelope carousel = Component.Carousel()
    .WithTitle("Continue Watching")
    .WithItems(continueWatching)
    .WithUpdate("pageLoad", "/home/continue")
    ;
```

## File Structure

```
Components/
├── ComponentTypes.cs        - Component type constants
├── IComponentProps.cs       - Base interfaces
├── ComponentEnvelope.cs     - Main wrapper type
├── ContainerProps.cs        - Container component props
├── LeafProps.cs            - Leaf component props
├── ComponentFactory.cs      - Fluent builders
├── ComponentResponse.cs     - API response wrappers
├── ComponentExtensions.cs   - Helper extensions
├── UpdateDto.cs            - Update configuration
├── ContextMenuItemDto.cs   - Context menu items
├── CardData.cs             - NMCard data
├── HomeCardData.cs         - NMHomeCard data
├── GenreCardData.cs        - NMGenreCard data
├── MusicCardData.cs        - NMMusicCard/NMMusicHomeCard data
├── TrackRowData.cs         - NMTrackRow data
└── TopResultCardData.cs    - NMTopResultCard data
```

## JSON Structure

The component envelope serializes to:

```json
{
  "id": "01ARZ3NDEKTSV4RRFFQ69G5FAV",
  "component": "NMCarousel",
  "props": {
    "id": "recent-movies",
    "next_id": "genres-28",
    "previous_id": "continue-watching",
    "title": "Recent Movies",
    "more_link": "/movies",
    "more_link_text": "See all",
    "items": [
      {
        "id": "01ARZ3NDEKTSV4RRFFQ69G5FAW",
        "component": "NMCard",
        "props": {
          "id": "123",
          "data": {
            "id": 123,
            "title": "Movie Title",
            "poster": "/path/to/poster.jpg",
            ...
          },
          "watch": true
        }
      }
    ]
  },
  "update": {
    "when": "pageLoad",
    "link": "/home/recent",
    "body": { "replace_id": "01ARZ3NDEKTSV4RRFFQ69G5FAV" }
  }
}
```
