# Integration Tests

This integration test scripts require several secrets to exist:


- `DOCKERHUB_USERNAME`
    - The username used to authenticate to `dockerhub`. Also used as the prefix for the built image.
- `DOCKERHUB_TOKEN`
    - The access token for `dockerhub`. Requires pull/push permissions.
- `ORG_URL`
    - The ADO organization URL to use for the test
- `ORG_PAT`
    - The Personal Access Token for the ADO Organization. Requires the same permissions as for when provisoning an agent.