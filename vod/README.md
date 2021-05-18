![BCC.Media logo](https://storage.googleapis.com/bcc-media-public/bcc-media-logo-150.png)

# VOD functions

This project consists crucial assisting functions for the VOD streaming workflow.

- Hls proxy: Modify the HLS manifests. Most importantly it specifies where to retrieve decryption keys, but it also customizes the manifests.

## Expanding this project

Be very mindful of what you add to this project.
Because VOD streaming is one of our key components, we want this project to be lightweight and only include functionality that is necessary for the playback of VOD streams.
This is in order to minimize risk of downtime and load issues.
