# TODO

## GitHub Publication

- [x] Create new GitHub repository at https://github.com/JHoahg/JellyPodcast
- [x] Update repository URLs in documentation
- [ ] Push local repository to GitHub
  ```bash
  git remote add origin https://github.com/JHoahg/JellyPodcast.git
  git branch -M main
  git push -u origin main
  ```

- [ ] Add repository topics/tags on GitHub
  - jellyfin
  - jellyfin-plugin
  - podcast
  - rss
  - atom
  - media-server

- [ ] Create GitHub release
  - Tag version: v1.0.0
  - Upload compiled DLL as release asset
  - Include installation instructions in release notes

## Future Enhancements

- [ ] Add OPML import/export for feed management
- [ ] Custom refresh intervals per feed
- [ ] Episode filtering (by date range, keywords)
- [ ] Download episodes locally instead of streaming
- [ ] Support for podcast chapters
- [ ] Feed authentication support (HTTP Basic Auth)
- [ ] Integration with podcast directories for discovery
- [ ] Support for more metadata fields (categories, explicit content flags)
- [ ] Multi-language support for UI
- [ ] Episode playback statistics and tracking

## Known Issues to Address

- [ ] ZEIT Feed Images: Some ZEIT podcast feeds provide image URLs instead of audio URLs
- [ ] Fixed refresh interval (not user-configurable yet)
- [ ] No support for password-protected feeds
- [ ] Video podcast metadata handling could be improved
