## ADDED Requirements

### Requirement: Generate client methods for supported inline response schemas

The system SHALL generate client methods for operations whose success responses use supported inline schemas (object/primitive/supported array forms).

#### Scenario: Supported inline response object produces typed method
- **WHEN** an operation has a supported inline object success response schema
- **THEN** the generated client method is emitted with `Task<GeneratedInlineType>` return type
- **THEN** the operation is not dropped from generated interfaces/implementations

#### Scenario: Supported inline primitive response produces typed method
- **WHEN** an operation has a supported inline primitive success response schema (for example `type: string`)
- **THEN** the generated client method is emitted with the mapped primitive return type (for example `Task<string>`)
- **THEN** the operation is not dropped from generated interfaces/implementations

#### Scenario: JSON string response body is quoted
- **WHEN** an operation success response type is `string` and response body is a valid quoted JSON string
- **THEN** generated client returns the unquoted/unescaped string value

#### Scenario: JSON string response body is unquoted raw text
- **WHEN** an operation success response type is `string` and server returns unquoted raw text body despite JSON content-type
- **THEN** generated client returns raw text instead of throwing deserialization failure

#### Scenario: Unsupported inline response composition remains skipped
- **WHEN** an operation success response uses unsupported inline composition
- **THEN** the method is skipped
- **THEN** warning diagnostic `AS401` remains emitted for that operation
