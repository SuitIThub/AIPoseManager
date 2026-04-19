class PluginError(Exception):
    """Base class for plugin HTTP errors."""

    def __init__(self, message: str, detail: str | None = None):
        super().__init__(message)
        self.detail = detail or message


class PluginUnauthorized(PluginError):
    pass


class PluginNotFound(PluginError):
    pass


class PluginBadRequest(PluginError):
    pass


class PluginInternal(PluginError):
    pass


class PluginConnectionError(PluginError):
    pass
