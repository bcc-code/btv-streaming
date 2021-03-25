![BCC.Media logo](https://storage.googleapis.com/bcc-media-public/bcc-media-logo-150.png)

# Livestream functions

There are 3 components in this project:

- Urls: Deliver urls to the stream, in exchange for a bcc-signed jwt.
- Hls proxy: Modify the HLS manifests. Most importantly, it specifies where to retrieve decryption keys.
- KeyDelivery: Delivers decryption keys in exchange for a streaming jwt.
