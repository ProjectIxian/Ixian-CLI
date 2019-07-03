# Ixian-CLI
Ixian command line client for DLT Node API.

## Usage
ixicli [-h] [-v] [-i ADDRESS] [-p PORT] [-u USERNAME] [-w PASSWORD] command [arguments]

## Parameters
| Short | Long version | Description |
| -h | n/a | Display short help information |
| -v | n/a | Verbose mode. Displays more information about what the client is doing. |
| -i | --ip | Set DLTNode IP or hostname. If omitted, `localhost` is assumed. |
| -p | --port | Set DLTNode API port. If omitted, the default value of `8081` is used. |
| -u | --username | Set DLTNode API username, if the node requires authentication. |
| -w | --password | Set DLTNode API password, if the node requries authetnication. |
| n/a| --pretty | Prints the JSON result in a more human-readable form. |
| n/a| --stdin | Allow passing arguments via the command line, to avoid possible shell limits. |

## Calling API methods
The first argument without the `-` symbol is considered a method name. It should consist only of alphanumeric characters and underlines,
with no special characters.

Examples: `getblock`, `status`, `txu`

## Method parameters
Some API methods require additional parameters. Any such values may be supplied on the command-line in the form:

`name=value`

In addition, longer arguments may also be supplied via the standard input, if the option `--stdin` is specified. In this case,
arguments are parsed in the same way as on the program command line (multiple arguments per line are allowed). Parsing is finished
when an empty line is encountered.

ixicli does not perform any kind of validation on these parameters and passes them on to the Node's API server for processing.

If duplicate arguments are specified, the later values will overwrite the earlier values with the same key.
Note: key names are case-sensitive.