# FinanceMaker

## Environment Variables for IB Gateway

The `ib-gateway` service requires several environment variables for configuration. **Do not commit secrets to the repository.**

### Local Development
Create a `.env` file in the root of your repository (add `.env` to `.gitignore`). Example:

```
TWS_USERID=your_user
TWS_PASSWORD=your_password
TRADING_MODE=paper
# Add other variables as needed
```

`docker-compose` will automatically use this file to populate environment variables for the `ib-gateway` service.

### CI/CD (GitHub Actions)
Add the required environment variables as GitHub Secrets (in your repository settings under `Settings > Secrets and variables > Actions`).

- The workflow will use these secrets when building and deploying your services.
- Reference the variable names as shown in `docker-compose.yml`.

### Reference
See the `docker-compose.yml` file for the full list of supported environment variables for `ib-gateway`.
