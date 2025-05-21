# Fuzzing

This is a small fuzzing setup to test the URL parser. 
It uses SharpFuzz and AFL++

Seeds.txt is currently generated from urltestdata.json

## Usage

From the root of the project, run the following command to start fuzzing

```bash
docker compose -f ./docker-compose.fuzzing.yml run fuzzing
```