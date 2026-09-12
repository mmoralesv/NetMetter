# Store listing

`listing.json` holds the Microsoft Store listing metadata (description, release notes,
screenshot captions, etc.) so listing changes can be reviewed and shipped from git.

It is **not committed yet** because its structure comes from Partner Center. To create it:

1. Run the **Store listing** workflow from the Actions tab with action **`get`**.
2. Copy the submission JSON it prints from the run logs.
3. Save it here as `store/listing.json` and edit the text you want to change.
4. Push to `main`. The **Store listing** workflow then applies and publishes it.

Package (binary) updates go through **Release** (`release.yml`), not this file.
