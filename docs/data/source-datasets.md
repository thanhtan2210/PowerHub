# Source Dataset Strategy

## Objective

PowerHub currently has no physical test hardware and no owned telemetry. The initial evidence strategy therefore combines measured public datasets with a Virtual Device Simulator. This validates ingestion, storage, APIs, user workflows, failure handling, and performance, but it does not certify electrical hardware, firmware, sensors, radio behavior, or physical safety.

## Approved initial sources

| Priority | Dataset | Intended use | Key characteristics | License note |
| --- | --- | --- | --- | --- |
| Primary | REFIT Electrical Load Measurements | Appliance and household replay through MQTT | 20 UK households, aggregate and appliance channels, nominal 8-second sampling over approximately two years | Dataset page declares CC BY 4.0 |
| Developer fixture | UCI Appliances Energy Prediction | Small repeatable local and CI scenarios | Energy, temperature, humidity, weather-derived fields; approximately 4.5 months at 10-minute intervals | UCI page declares CC BY 4.0 |
| Resilience | UCI Individual Household Electric Power Consumption | Missing, long-duration, and one-minute series tests | 2,075,259 one-minute measurements over 47 months with documented missing values | UCI page declares CC BY 4.0 |
| Performance candidate | Building Data Genome Project 2 | Large-scale aggregation and query testing | 3,053 meters across 1,636 non-residential buildings, two years of hourly readings | Verify artifact and redistribution terms before automation |
| Optional external | NOAA GHCNh | Live or historical weather correlation | Global hourly station observations | Review provider terms and attribution for the selected access path |

Authoritative references:

- [REFIT dataset record](https://pureportal.strath.ac.uk/en/datasets/refit-electrical-load-measurements/)
- [UCI Appliances Energy Prediction](https://archive.ics.uci.edu/dataset/374/appliances%2Benergy%2Bprediction)
- [UCI Individual Household Electric Power Consumption](https://archive.ics.uci.edu/dataset/235/individual%2Bhousehold%2Belectric%2Bpower%2Bci)
- [Building Data Genome Project 2 archive](https://zenodo.org/records/3887306)
- [Building Data Genome Project 2 paper](https://doi.org/10.1038/s41597-020-00712-x)
- [NOAA Global Historical Climatology Network hourly data](https://www.ncei.noaa.gov/products/global-historical-climatology-network-hourly)

## Dataset admission checklist

A dataset is not added to automated workflows until the following are recorded:

- authoritative source URL and retrieval date;
- license, attribution, and redistribution constraints;
- immutable local artifact checksum and upstream version;
- geography, time range, sampling interval, units, and channel meanings;
- missing-value and anomaly behavior;
- transformation mapping to the MQTT schema;
- privacy or re-identification assessment;
- intended environments and test cases;
- owner and review date.

If licensing is ambiguous, store only retrieval instructions and checksums in the repository, not the dataset artifact.

## Repository policy

- Large raw datasets are not committed to Git.
- Downloaded artifacts use an ignored local cache or approved object storage.
- CI uses a small, licensed, checksum-pinned fixture derived according to the source terms.
- Secrets or paid-provider credentials are never embedded in download scripts.
- Derived fixtures retain source attribution, transformation version, and provenance.

## Mapping strategy

External household or appliance channels map to stable simulated device identifiers. Source timestamps may be replayed in original time or shifted to a configured start while preserving intervals. Replay speed changes wall-clock delay, not measurement meaning.

Each outbound MQTT record carries:

- source dataset key and immutable version;
- opaque source row reference;
- original or shifted observation timestamp;
- canonical measurement names and units;
- `real_replay` provenance;
- stable message and device identifiers for reproducibility.

## Selection by test level

- Unit tests: synthetic values only, because exact edge conditions must be deterministic.
- Contract tests: a tiny, approved real-data fixture plus malformed synthetic messages.
- Local integration: UCI Appliances fixture by default.
- Staging acceptance: representative REFIT replay.
- Resilience tests: UCI Household gaps, duplicates, late data, and restarts.
- Performance tests: scaled REFIT first; consider Building Data Genome 2 only after license review.

## Evidence boundary

Dataset replay can demonstrate software behavior and provide reproducible performance results. Before claiming support for a physical product, PowerHub still requires a hardware validation phase covering device provisioning, sensor accuracy, electrical behavior, connectivity loss, firmware updates, and safety-related requirements.
