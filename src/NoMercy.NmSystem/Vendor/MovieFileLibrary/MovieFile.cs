// Vendored from MovieFileLibrary 3.1.0 (https://github.com/moviecollection/movie-file-library)
// Copyright (c) Peyman Mohammadi — Licensed under the MIT License.
// Imported into this repository to own the parsing logic and named regexes; behavior preserved.

﻿namespace MovieFileLibrary
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    /// <summary>
    /// A <c>MovieFile</c> represents information about a movie file.
    /// </summary>
    public class MovieFile
    {
        private readonly List<int> _episodes = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="MovieFile"/> class.
        /// </summary>
        /// <param name="filePath">The path of a movie file.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="filePath"/> is null or whitespace.
        /// </exception>
        public MovieFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(value: filePath))
            {
                throw new ArgumentException(message: $"'{nameof(filePath)}' cannot be null or whitespace", paramName: nameof(filePath));
            }

            Path = filePath;
            Extension = System.IO.Path.GetExtension(path: filePath);
        }

        /// <summary>
        /// Gets or sets movie title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets movie year.
        /// </summary>
        public string? Year { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether movie file is a series.
        /// </summary>
        public bool IsSeries { get; set; }

        /// <summary>
        /// Gets or sets series season number if exists.
        /// </summary>
        public int? Season { get; set; }

        /// <summary>
        /// Gets the series episode number or null.
        /// </summary>
        public int? Episode
        {
            get => _episodes.Count == 0 ? null : _episodes[index: 0];
            set
            {
                _episodes.Clear();
                if (value.HasValue)
                {
                    _episodes.Add(item: value.Value);
                }
            }
        }

        /// <summary>
        /// Gets or sets imdb id if exists.
        /// </summary>
        public string? ImdbId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether it's a special episode.
        /// </summary>
        public bool IsSpecialEpisode { get; set; }

        /// <summary>
        /// Gets the series episode numbers.
        /// </summary>
        public ReadOnlyCollection<int> Episodes => _episodes.AsReadOnly();

        /// <summary>
        /// Gets or sets a value indicating whether detection was successful.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets path of the movie file.
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// Gets extension of the movie file.
        /// </summary>
        public string? Extension { get; private set; }

        /// <summary>
        /// Adds an episode to the end of the episode numbers collection.
        /// </summary>
        /// <param name="episode">The episode number.</param>
        public void AddEpisode(int episode)
        {
            _episodes.Add(item: episode);
        }
    }
}
