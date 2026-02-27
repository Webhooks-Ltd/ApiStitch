## ADDED Requirements

### Requirement: Map content types to ContentKind enum values

The system SHALL define a `ContentKind` enum with values `Json`, `FormUrlEncoded`, `MultipartFormData`, `OctetStream`, and `PlainText`. The system SHALL map OpenAPI media type strings to the corresponding `ContentKind` value on `ApiRequestBody.ContentKind` and `ApiResponse.ContentKind`. The original media type string SHALL be preserved in a separate `MediaType` property.

#### Scenario: application/json maps to Json
- **WHEN** an operation has request body content type `application/json`
- **THEN** `ApiRequestBody.ContentKind` = `Json` and `ApiRequestBody.MediaType` = `"application/json"`

#### Scenario: Vendor JSON media type maps to Json
- **WHEN** an operation has request body content type `application/vnd.api+json`
- **THEN** `ApiRequestBody.ContentKind` = `Json` and `ApiRequestBody.MediaType` = `"application/vnd.api+json"`

#### Scenario: application/x-www-form-urlencoded maps to FormUrlEncoded
- **WHEN** an operation has request body content type `application/x-www-form-urlencoded`
- **THEN** `ApiRequestBody.ContentKind` = `FormUrlEncoded` and `ApiRequestBody.MediaType` = `"application/x-www-form-urlencoded"`

#### Scenario: multipart/form-data maps to MultipartFormData
- **WHEN** an operation has request body content type `multipart/form-data`
- **THEN** `ApiRequestBody.ContentKind` = `MultipartFormData` and `ApiRequestBody.MediaType` = `"multipart/form-data"`

#### Scenario: application/octet-stream maps to OctetStream
- **WHEN** an operation has request body content type `application/octet-stream`
- **THEN** `ApiRequestBody.ContentKind` = `OctetStream` and `ApiRequestBody.MediaType` = `"application/octet-stream"`

#### Scenario: application/pdf response maps to OctetStream
- **WHEN** an operation has a success response with content type `application/pdf`
- **THEN** `ApiResponse.ContentKind` = `OctetStream` and `ApiResponse.MediaType` = `"application/pdf"`

#### Scenario: image/* response maps to OctetStream
- **WHEN** an operation has a success response with content type `image/png`
- **THEN** `ApiResponse.ContentKind` = `OctetStream` and `ApiResponse.MediaType` = `"image/png"`

#### Scenario: text/plain maps to PlainText
- **WHEN** an operation has request body content type `text/plain`
- **THEN** `ApiRequestBody.ContentKind` = `PlainText` and `ApiRequestBody.MediaType` = `"text/plain"`

#### Scenario: Unsupported content type still emits AS404
- **WHEN** an operation has request body content type `application/xml`
- **THEN** the request body is skipped
- **THEN** a warning diagnostic with code `AS404` is emitted

### Requirement: Select preferred content type when multiple are offered

The system SHALL select one content type when an operation's request body offers multiple, using the preference order: JSON > FormUrlEncoded > MultipartFormData > OctetStream > PlainText. The system SHALL emit an `AS409` info diagnostic listing the selected content type and the skipped alternatives.

#### Scenario: JSON preferred over form-encoded
- **WHEN** an operation offers `application/json` and `application/x-www-form-urlencoded` for the request body
- **THEN** `ApiRequestBody.ContentKind` = `Json`
- **THEN** an info diagnostic with code `AS409` is emitted indicating `application/json` was selected

#### Scenario: Form-encoded selected when JSON not available
- **WHEN** an operation offers `application/x-www-form-urlencoded` and `multipart/form-data` for the request body
- **THEN** `ApiRequestBody.ContentKind` = `FormUrlEncoded`
- **THEN** an info diagnostic with code `AS409` is emitted indicating `application/x-www-form-urlencoded` was selected

#### Scenario: Multipart selected when neither JSON nor form available
- **WHEN** an operation offers `multipart/form-data` and `application/octet-stream` for the request body
- **THEN** `ApiRequestBody.ContentKind` = `MultipartFormData`
- **THEN** an info diagnostic with code `AS409` is emitted

#### Scenario: Single content type produces no negotiation diagnostic
- **WHEN** an operation offers only `application/json` for the request body
- **THEN** `ApiRequestBody.ContentKind` = `Json`
- **THEN** no `AS409` diagnostic is emitted

#### Scenario: All offered types unsupported
- **WHEN** an operation offers only `application/xml` and `application/msgpack` for the request body
- **THEN** the request body is skipped
- **THEN** a warning diagnostic with code `AS404` is emitted for each unsupported type

### Requirement: Generate Accept header on requests

The system SHALL emit an `Accept` header on every generated HTTP request, with the value taken from `ApiResponse.MediaType` of the operation's success response. No `Accept` header SHALL be set for operations with no response body.

#### Scenario: JSON response sets Accept header
- **WHEN** an operation has a success response with `MediaType` = `"application/json"`
- **THEN** the generated method includes `request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"))`

#### Scenario: PDF response sets specific Accept header
- **WHEN** an operation has a success response with `MediaType` = `"application/pdf"`
- **THEN** the generated method includes `request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/pdf"))`

#### Scenario: Plain text response sets Accept header
- **WHEN** an operation has a success response with `MediaType` = `"text/plain"`
- **THEN** the generated method includes `request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"))`

#### Scenario: No-content response omits Accept header
- **WHEN** an operation has a 204 success response with no body
- **THEN** the generated method does NOT set an `Accept` header

#### Scenario: No-content response has null ContentKind and MediaType
- **WHEN** an operation has a 204 success response with no content
- **THEN** `ApiResponse.ContentKind` is null
- **THEN** `ApiResponse.MediaType` is null
- **THEN** `ApiResponse.HasBody` is false

### Requirement: Validate response Content-Type before deserialization

The system SHALL check the response `Content-Type` header before attempting JSON deserialization. If the response `Content-Type` is not `application/json` and does not end with `+json` (case-insensitive), the system SHALL throw an `ApiException` with a descriptive message including the actual content type and response body. This check SHALL only apply to the JSON deserialization code path, not to stream or plain text responses.

#### Scenario: Valid JSON content type passes validation
- **WHEN** a JSON response is received with `Content-Type: application/json`
- **THEN** deserialization proceeds normally

#### Scenario: Vendor JSON content type passes validation
- **WHEN** a JSON response is received with `Content-Type: application/vnd.api+json`
- **THEN** deserialization proceeds normally (the `+json` suffix matches)

#### Scenario: Null content type passes validation
- **WHEN** a JSON response is received with no `Content-Type` header
- **THEN** deserialization proceeds normally (null content type is not rejected)

#### Scenario: HTML content type throws ApiException
- **WHEN** a 200 response is received but `Content-Type` is `text/html`
- **THEN** an `ApiException` is thrown
- **THEN** the exception message MUST contain the string `"text/html"` and the response body content

#### Scenario: XML content type throws ApiException
- **WHEN** a 200 response is received but `Content-Type` is `application/xml`
- **THEN** an `ApiException` is thrown with a message containing `"application/xml"`

#### Scenario: Validation not applied to stream responses
- **WHEN** an operation returns `FileResponse` (stream response)
- **THEN** no Content-Type validation is performed before returning the stream

#### Scenario: Validation not applied to plain text responses
- **WHEN** an operation returns a `string` (plain text response)
- **THEN** no Content-Type validation is performed before calling `ReadAsStringAsync`
