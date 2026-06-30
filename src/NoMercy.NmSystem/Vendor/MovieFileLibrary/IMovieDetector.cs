// Vendored from MovieFileLibrary 3.1.0 (https://github.com/moviecollection/movie-file-library)
// Copyright (c) Peyman Mohammadi — Licensed under the MIT License.
// Imported into this repository to own the parsing logic and named regexes; behavior preserved.

﻿namespace MovieFileLibrary
{
    /// <summary>
    /// The <c>IMovieDetector</c> interface.
    /// </summary>
    public interface IMovieDetector
    {
        /// <summary>
        /// Gets movie info from a filename.
        /// </summary>
        /// <param name="filePath">The path of a movie file.</param>
        /// <returns>New instance of the <see cref="MovieFile"/> class with basic info regarding the movie.</returns>
        /// <exception cref="System.ArgumentException">
        /// Thrown when <paramref name="filePath"/> is null or whitespace.
        /// </exception>
        MovieFile GetInfo(string filePath);
    }
}
