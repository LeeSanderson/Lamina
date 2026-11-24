# Lamina

Lamina is four-layer acceptance testing for .NET: specs in domain language, DSLs, drivers, and the system under test. This glossary holds the terms the project has sharpened; Plan.md §3 holds the rest until they are.

## Language

### System under test

**System under test (SUT)**:
The deployed system a suite's specs exercise, seen only from outside through its endpoints.
_Avoid_: app, environment, target

**SUT provider**:
The part of a suite that starts or locates the system under test once per run and reports its endpoints. Which provider a run uses is chosen by configuration, never by code.
_Avoid_: host, launcher, SUT mode

**Endpoint**:
A named address on the system under test, named after the deployable unit or stub it reaches, such as `web-bff` or `payment-provider`. The same name is used however the system was started.
_Avoid_: service URL, base address

**Run start**:
The moment after the system under test is ready and before any spec runs, when run-level set-up such as stub defaults happens once.
_Avoid_: global setup, bootstrap

### Channels and DSLs

**Default channel**:
The suite's fastest channel, through which arrangement always runs and on which a capability with no driver for the spec's channel falls back.
_Avoid_: primary channel, base channel

**Actor DSL**:
A DSL whose steps run through the spec's own channel, because they are the behaviour under test.
_Avoid_: channel DSL, behaviour DSL

**Arrangement DSL**:
A DSL pinned to the default channel, whatever the spec's channel: it puts the system into the state a spec starts from, and may check outcomes the spec's channel needn't show. It acts as its own arranging user, never as the actor the spec signed in as.
_Avoid_: setup DSL, fixture DSL, pinned DSL

**Arrangement pinning**:
Running every step of an arrangement DSL through the default channel, so setup stays fast and the spec's channel is spent only on the behaviour under test.
_Avoid_: channel override
